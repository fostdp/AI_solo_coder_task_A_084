
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TextileMonitoring.Infrastructure.Configuration
{
    public static class ConfigurationExtensions
    {
        public static IHostBuilder ConfigureTextileMonitoring(this IHostBuilder host)
        {
            host.ConfigureAppConfiguration((context, config) =>
            {
                var env = context.HostingEnvironment;

                config.SetBasePath(AppContext.BaseDirectory)
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                      .AddEnvironmentVariables("TEXTILE_")
                      .AddCommandLine(System.Environment.GetCommandLineArgs().Skip(1).ToArray());

                if (env.IsDevelopment())
                {
                    config.AddUserSecrets(System.Reflection.Assembly.GetEntryAssembly(), optional: true);
                }
            });

            host.UseSerilog((context, services, loggerConfig) =>
            {
                loggerConfig
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("ServiceName", System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown")
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: "logs/log-.txt",
                        rollingInterval: Serilog.RollingInterval.Day,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ServiceName}] {Message:lj}{NewLine}{Exception}");
            });

            return host;
        }

        public static IServiceCollection AddTextileMonitoringConfig(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<AppConfig>(config);
            services.Configure<RabbitMqConfig>(config.GetSection("RabbitMq"));
            services.Configure<DatabaseConfig>(config.GetSection("Database"));
            services.Configure<ZigBeeConfig>(config.GetSection("ZigBee"));
            services.Configure<PopulationModelConfig>(config.GetSection("PopulationModel"));
            services.Configure<MildewModelConfig>(config.GetSection("MildewModel"));
            services.Configure<AlertThresholdsConfig>(config.GetSection("AlertThresholds"));
            services.Configure<NotificationConfig>(config.GetSection("Notifications"));
            services.Configure<ServiceEndpointsConfig>(config.GetSection("ServiceEndpoints"));

            services.AddSingleton(sp =>
            {
                var appConfig = new AppConfig();
                config.Bind(appConfig);
                return appConfig;
            });

            return services;
        }

        public static AppConfig GetTextileMonitoringConfig(this IConfiguration config)
        {
            var appConfig = new AppConfig();
            config.Bind(appConfig);
            return appConfig;
        }

        public static string GetRequiredConnectionString(this IConfiguration config, string name)
        {
            var cs = config.GetConnectionString(name);
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException($"Connection string '{name}' is not configured. " +
                    $"Set environment variable TEXTILE_ConnectionStrings__{name} or configure in appsettings.json");
            return cs;
        }
    }
}
