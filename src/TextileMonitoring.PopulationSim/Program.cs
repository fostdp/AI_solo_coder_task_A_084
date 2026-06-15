using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TextileMonitoring.Infrastructure.Configuration;
using TextileMonitoring.Infrastructure.Messaging;
using TextileMonitoring.PopulationSim;
using TextileMonitoring.PopulationSim.Data;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Population Simulation Service...");

    var builder = Host.CreateDefaultBuilder(args);

    builder.ConfigureTextileMonitoring();

    builder.ConfigureServices((context, services) =>
    {
        var config = context.Configuration;
        var appConfig = config.GetTextileMonitoringConfig();

        services.AddTextileMonitoringConfig(config);

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = config.GetRequiredConnectionString("DefaultConnection");
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(appConfig.Database.CommandTimeout);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: appConfig.Database.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(appConfig.Database.MaxRetryDelaySec),
                    errorNumbersToAdd: null);
            });
        });

        services.AddTransient<LotkaVolterraPredictionService>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<SensorDataConsumer>();

            x.SetKebabCaseEndpointNameFormatter();
            x.SetSnakeCaseEntityNameFormatter();

            x.UsingRabbitMq((busContext, cfg) =>
            {
                var rabbitConfig = busContext.GetRequiredService<Microsoft.Extensions.Options.IOptions<RabbitMqConfig>>().Value;

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
                        $"textile-monitoring.sensor.{ctx.Message.SensorType.ToString().ToLowerInvariant()}");
                });

                cfg.Publish<TextileMonitoring.Messages.Events.ISensorDataReceived>(p =>
                {
                    p.ExchangeType = "topic";
                });

                cfg.ReceiveEndpoint("textile-monitoring.population-sim.sensor-data", e =>
                {
                    e.PrefetchCount = rabbitConfig.PrefetchCount;
                    e.UseMessageRetry(r => r.Immediate(3));
                    e.UseDeadLetterQueue("textile-monitoring.population-sim.sensor-data.dlq");
                    e.Bind("textile-monitoring.sensor.dust", x => x.RoutingKey = "textile-monitoring.sensor.*");
                    e.ConfigureConsumer<SensorDataConsumer>(busContext);
                });
            });
        });

        services.AddMassTransitHostedService();
        services.AddSingleton<IMessageBus, MassTransitMessageBus>();

        services.AddHealthChecks();
    });

    var host = builder.Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Population Simulation Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
