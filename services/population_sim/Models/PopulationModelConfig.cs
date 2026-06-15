namespace PopulationSim.Service.Models;

public class PopulationModelConfig
{
    public string DefaultModel { get; set; } = "LotkaVolterra";
    public double DefaultGrowthRate { get; set; } = 0.05;
    public double DefaultCarryingCapacity { get; set; } = 12.0;
    public double DefaultInitialPestDensity { get; set; } = 0.5;
    public double AlphaPredation { get; set; } = 0.35;
    public double BetaConversion { get; set; } = 0.02;
    public double DeltaMortality { get; set; } = 0.08;
    public double DefaultInitialPredatorDensity { get; set; } = 0.15;
    public double PredatorCarryingCapacity { get; set; } = 2.5;
    public double SynergyGamma { get; set; } = 0.0003;
    public int DefaultHorizonDays { get; set; } = 30;
    public double OdeInitialStep { get; set; } = 0.1;
    public double OdeMinStep { get; set; } = 0.005;
    public double OdeMaxStep { get; set; } = 0.5;
    public double DivergenceThreshold { get; set; } = 0.5;
    public double AutoTuneCoefficient { get; set; } = 0.08;
    public double TemperatureOptimal { get; set; } = 22.0;
    public double HumidityOptimal { get; set; } = 55.0;
    public double PredatorDensityWarning { get; set; } = 0.3;
    public double PestDensityWarning { get; set; } = 3.0;
    public double PestDensityCritical { get; set; } = 5.0;
}

public class PredictionWindowConfig
{
    public int WindowMinutes { get; set; } = 10;
    public int MinDataPoints { get; set; } = 3;
    public int MaxBatchSize { get; set; } = 100;
}

public class RabbitMqConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public ushort PrefetchCount { get; set; } = 32;
    public int RetryCount { get; set; } = 5;
    public int RetryIntervalMs { get; set; } = 1000;
}

public class DatabaseConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeout { get; set; } = 120;
    public int MaxRetryCount { get; set; } = 5;
    public int MaxRetryDelaySec { get; set; } = 10;
    public bool EnableDetailedErrors { get; set; } = false;
    public bool EnableSensitiveDataLogging { get; set; } = false;
}
