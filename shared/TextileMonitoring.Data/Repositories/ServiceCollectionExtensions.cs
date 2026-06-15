
namespace TextileMonitoring.Data.Repositories;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddDataRepositories(this IServiceCollection services)
    {
        services.AddScoped<ITextileRepository, TextileRepository>();
        services.AddScoped<ISensorDataRepository, SensorDataRepository>();
        return services;
    }

    public static IServiceCollection AddTextileDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TextileMonitoringDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(120);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });
        });
        return services;
    }
}
