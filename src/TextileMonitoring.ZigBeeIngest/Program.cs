
using Microsoft.EntityFrameworkCore;
using Serilog;
using TextileMonitoring.Infrastructure.Configuration;
using TextileMonitoring.Infrastructure.Messaging;
using TextileMonitoring.ZigBeeIngest;
using TextileMonitoring.ZigBeeIngest.Data;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ZigBee Ingest Service...");

    var builder = Host.CreateDefaultBuilder(args);

    builder.ConfigureTextileMonitoring();

    builder.ConfigureServices((context, services) =>
    {
        var config = context.Configuration;
        var appConfig = config.GetTextileMonitoringConfig();

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

        services.AddTransient<ISensorDataAdapter, SensorDataAdapter>();

        services.AddRabbitMqMessageBus("zigbee-ingest");

        services.AddHostedService<ZigBeeListenerWorker>();

        services.AddHealthChecks();
    });

    var host = builder.Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ZigBee Ingest Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
