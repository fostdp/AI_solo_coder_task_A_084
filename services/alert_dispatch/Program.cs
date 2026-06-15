using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TextileMonitoring.AlertDispatch.Consumers;
using TextileMonitoring.AlertDispatch.Models;
using TextileMonitoring.AlertDispatch.Services;
using TextileMonitoring.Contracts.Messages;
using TextileMonitoring.Data;
using TextileMonitoring.Infrastructure.Configuration;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Alert Dispatch Service...");

    var builder = Host.CreateDefaultBuilder(args);

    builder.ConfigureTextileMonitoring();

    builder.ConfigureServices((context, services) =>
    {
        var config = context.Configuration;
        var appConfig = config.GetTextileMonitoringConfig();

        services.AddTextileMonitoringConfig(config);

        services.Configure<AlertDispatchOptions>(config.GetSection("AlertDispatch"));

        services.AddDbContext<TextileMonitoringDbContext>(options =>
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

        services.AddHttpClient<IDingTalkNotifier, DingTalkNotifier>();
        services.AddTransient<IEmailNotifier, EmailNotifier>();
        services.AddTransient<IAlertRepository, AlertRepository>();
        services.AddTransient<IAlertDispatchService, AlertDispatchService>();
        services.AddSingleton(Log.Logger);

        services.AddMassTransit(x =>
        {
            x.AddConsumer<AlertTriggeredConsumer, AlertTriggeredConsumerDefinition>();

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

                cfg.Send<AlertTriggered>(s =>
                {
                    s.UseCorrelationId(ctx => ctx.CorrelationId);
                });

                cfg.Publish<AlertDispatched>(p =>
                {
                    p.ExchangeType = "topic";
                });

                cfg.ReceiveEndpoint("textile-monitoring.alert-dispatch.alert-triggered", e =>
                {
                    e.PrefetchCount = rabbitConfig.PrefetchCount;
                    e.UseMessageRetry(r => r.Immediate(3));
                    e.UseDeadLetterQueue("textile-monitoring.alert-dispatch.alert-triggered.dlq");
                    e.ConfigureConsumer<AlertTriggeredConsumer>(busContext);
                });

                cfg.ReceiveEndpoint("textile-monitoring.alert-dispatch.dead-letter", e =>
                {
                    e.Handler<AlertTriggered>(async ctx =>
                    {
                        var logger = ctx.GetService<Serilog.ILogger>() ?? Serilog.Log.Logger;
                        logger.Error("Dead letter received for AlertTriggered: {@Data}",
                            new { ctx.Message.CorrelationId, ctx.Message.AlertLevel, ctx.Message.TextileName });
                        await Task.CompletedTask;
                    });
                });
            });
        });

        services.AddMassTransitHostedService();
        services.AddHealthChecks();
    });

    var host = builder.Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Alert Dispatch Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
