
using System.Reflection;
using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TextileMonitoring.Infrastructure.Configuration;

namespace TextileMonitoring.Infrastructure.Messaging
{
    public static class RabbitMqBusExtensions
    {
        private const string ExchangePrefix = "textile-monitoring";
        private const string QueuePrefix = "textile-monitoring";

        public static IServiceCollection AddRabbitMqMessageBus(
            this IServiceCollection services,
            string serviceName,
            Action<IRabbitMqBusFactoryConfigurator>? configureConsumers = null)
        {
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                x.SetSnakeCaseEntityNameFormatter();

                x.UsingRabbitMq((context, cfg) =>
                {
                    var rabbitConfig = context.GetRequiredService<Microsoft.Extensions.Options.IOptions<RabbitMqConfig>>().Value;

                    cfg.Host(rabbitConfig.Host, rabbitConfig.Port, rabbitConfig.VirtualHost, h =>
                    {
                        h.Username(rabbitConfig.Username);
                        h.Password(rabbitConfig.Password);
                        h.Heartbeat(TimeSpan.FromSeconds(30));
                    });

                    cfg.UseMessageRetry(r => r.Exponential(
                        retryLimit: rabbitConfig.RetryCount,
                        minDelay: TimeSpan.FromMilliseconds(100),
                        maxDelay: TimeSpan.FromMilliseconds(rabbitConfig.RetryIntervalMs * 5),
                        delta: TimeSpan.FromMilliseconds(rabbitConfig.RetryIntervalMs)));

                    cfg.UseCircuitBreaker(cb =>
                    {
                        cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                        cb.TripThreshold = 0.5;
                        cb.ActiveThreshold = 5;
                        cb.ResetInterval = TimeSpan.FromMinutes(1);
                    });

                    cfg.UseDelayedRedelivery(r => r.Intervals(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(30)));

                    cfg.ConfigureJsonSerializerOptions(opt =>
                    {
                        opt.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                    });

                    cfg.Send<TextileMonitoring.Messages.Events.ISensorDataReceived>(s =>
                    {
                        s.UseCorrelationId(ctx => ctx.CorrelationId);
                        s.UseRoutingKeyFormatter(ctx =>
                            $"{ExchangePrefix}.sensor.{ctx.Message.SensorType.ToString().ToLowerInvariant()}");
                    });

                    cfg.Publish<TextileMonitoring.Messages.Events.ISensorDataReceived>(p =>
                    {
                        p.ExchangeType = "topic";
                    });

                    cfg.ReceiveEndpoint($"{QueuePrefix}.{serviceName}.sensor-data", e =>
                    {
                        e.PrefetchCount = rabbitConfig.PrefetchCount;
                        e.UseMessageRetry(r => r.Immediate(3));
                        e.UseDeadLetterQueue($"{QueuePrefix}.{serviceName}.sensor-data.dlq");
                        e.Bind($"{ExchangePrefix}.sensor.dust", x => x.RoutingKey = $"{ExchangePrefix}.sensor.*");
                    });

                    configureConsumers?.Invoke(cfg);

                    if (serviceName == "zigbee-ingest")
                    {
                        cfg.ReceiveEndpoint($"{QueuePrefix}.{serviceName}.dead-letter", e =>
                        {
                            e.Handler<TextileMonitoring.Messages.Events.SensorDataReceived>(async ctx =>
                            {
                                var logger = ctx.GetService<Serilog.ILogger>() ?? Serilog.Log.Logger;
                                logger.Warning("Dead letter received: SensorData {@Data}",
                                    new { ctx.Message.CorrelationId, ctx.Message.SensorCode, ctx.Message.TextileId });
                                await Task.CompletedTask;
                            });
                        });
                    }
                });
            });

            services.AddMassTransitHostedService();
            services.AddSingleton<IMessageBus, MassTransitMessageBus>();

            return services;
        }

        public static IRabbitMqBusFactoryConfigurator ConfigureConsumer<T>(
            this IRabbitMqBusFactoryConfigurator cfg,
            IBusRegistrationContext context,
            string serviceName,
            string eventName,
            int prefetch = 16)
            where T : class, IConsumer
        {
            cfg.ReceiveEndpoint($"{QueuePrefix}.{serviceName}.{eventName}", e =>
            {
                e.PrefetchCount = (ushort)prefetch;
                e.UseMessageRetry(r => r.Immediate(3));
                e.UseDeadLetterQueue($"{QueuePrefix}.{serviceName}.{eventName}.dlq");
                e.ConfigureConsumer<T>(context);
            });
            return cfg;
        }
    }

    public interface IMessageBus
    {
        Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
        Task SendAsync<T>(T message, Uri endpointAddress, CancellationToken ct = default) where T : class;
    }

    public class MassTransitMessageBus : IMessageBus
    {
        private readonly IBus _bus;
        private readonly Serilog.ILogger _logger;

        public MassTransitMessageBus(IBus bus, Serilog.ILogger logger)
        {
            _bus = bus;
            _logger = logger;
        }

        public async Task PublishAsync<T>(T message, CancellationToken ct = default)
            where T : class
        {
            try
            {
                await _bus.Publish(message, ct);
                _logger.Verbose("Published {MessageType}: {@Message}", typeof(T).Name, message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to publish {MessageType}: {@Message}", typeof(T).Name, message);
                throw;
            }
        }

        public async Task SendAsync<T>(T message, Uri endpointAddress, CancellationToken ct = default)
            where T : class
        {
            try
            {
                var sendEndpoint = await _bus.GetSendEndpoint(endpointAddress);
                await sendEndpoint.Send(message, ct);
                _logger.Verbose("Sent {MessageType} to {Endpoint}: {@Message}",
                    typeof(T).Name, endpointAddress, message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send {MessageType} to {Endpoint}: {@Message}",
                    typeof(T).Name, endpointAddress, message);
                throw;
            }
        }
    }

    public static class MessageBusHealthCheck
    {
        public static async Task<bool> WaitForRabbitMqAsync(
            string host, int port, string username, string password,
            int maxAttempts = 30, int delayMs = 1000,
            CancellationToken ct = default)
        {
            var logger = Serilog.Log.Logger;
            int attempts = 0;

            while (!ct.IsCancellationRequested && attempts < maxAttempts)
            {
                try
                {
                    attempts++;
                    var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
                    {
                        cfg.Host(host, port, "/", h =>
                        {
                            h.Username(username);
                            h.Password(password);
                        });
                    });

                    await bus.StartAsync(ct);
                    await bus.StopAsync(ct);

                    logger.Information("RabbitMQ connection established after {Attempts} attempts", attempts);
                    return true;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "RabbitMQ connection attempt {Attempts}/{Max} failed",
                        attempts, maxAttempts);

                    if (attempts >= maxAttempts)
                    {
                        logger.Error("Failed to connect to RabbitMQ after {MaxAttempts} attempts", maxAttempts);
                        return false;
                    }

                    await Task.Delay(delayMs, ct);
                }
            }

            return false;
        }
    }
}
