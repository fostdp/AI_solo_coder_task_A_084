
namespace TextileMonitoring.Infrastructure.Configuration
{
    public class AppConfig
    {
        public RabbitMqConfig RabbitMq { get; set; } = new();
        public DatabaseConfig Database { get; set; } = new();
        public ZigBeeConfig ZigBee { get; set; } = new();
        public PopulationModelConfig PopulationModel { get; set; } = new();
        public MildewModelConfig MildewModel { get; set; } = new();
        public AlertThresholdsConfig AlertThresholds { get; set; } = new();
        public NotificationConfig Notifications { get; set; } = new();
        public ServiceEndpointsConfig ServiceEndpoints { get; set; } = new();
    }

    public class RabbitMqConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public ushort PrefetchCount { get; set; } = 16;
        public int RetryCount { get; set; } = 3;
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

    public class ZigBeeConfig
    {
        public int ListenPort { get; set; } = 8684;
        public int ReceiveTimeoutMs { get; set; } = 5000;
        public int BatchSize { get; set; } = 50;
        public int FlushIntervalMs { get; set; } = 10000;
        public string MulticastGroup { get; set; } = "239.255.86.84";
        public bool EnableMulticast { get; set; } = true;
    }

    public class PopulationModelConfig
    {
        public string DefaultModel { get; set; } = "LotkaVolterra";
        public double DefaultGrowthRate { get; set; } = 0.05;
        public double DefaultCarryingCapacity { get; set; } = 12.0;
        public double DefaultInitialPestDensity { get; set; } = 0.5;
        public double PredationEfficiencyAlpha { get; set; } = 0.35;
        public double PredatorConversionBeta { get; set; } = 0.02;
        public double PredatorMortalityDelta { get; set; } = 0.08;
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
    }

    public class MildewModelConfig
    {
        public string DefaultModel { get; set; } = "Gompertz";
        public double DefaultGrowthRateRho { get; set; } = 0.015;
        public double DefaultCarryingCapacityKf { get; set; } = 500.0;
        public double DefaultInitialCFU { get; set; } = 100.0;
        public double TemperatureSensitivity { get; set; } = 0.06;
        public double HumiditySensitivity { get; set; } = 0.025;
        public double SynergyInteractionPhi { get; set; } = 1.35;
        public double PestInteractionCoeff { get; set; } = 0.0005;
        public int DefaultHorizonDays { get; set; } = 30;
        public double MoldDetectionThreshold { get; set; } = 200.0;
        public double RadiusPerCFU { get; set; } = 0.125;
        public double MinRadiusMm { get; set; } = 15.0;
        public double MaxRadiusMm { get; set; } = 75.0;
    }

    public class AlertThresholdsConfig
    {
        public decimal HoleDensityWarning { get; set; } = 3.0m;
        public decimal HoleDensityCritical { get; set; } = 5.0m;
        public decimal FungiCFUWarning { get; set; } = 200.0m;
        public decimal FungiCFUCritical { get; set; } = 300.0m;
        public decimal SynergyRiskWarning { get; set; } = 50.0m;
        public decimal SynergyRiskCritical { get; set; } = 75.0m;
        public double PredatorDensityWarning { get; set; } = 0.3;
    }

    public class NotificationConfig
    {
        public DingTalkConfig DingTalk { get; set; } = new();
        public SmtpConfig Smtp { get; set; } = new();
        public List<string> EmailRecipients { get; set; } = new();
        public int CooldownMinutes { get; set; } = 60;
    }

    public class DingTalkConfig
    {
        public string Webhook { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public bool EnableAtAll { get; set; } = false;
        public List<string> AtMobiles { get; set; } = new();
    }

    public class SmtpConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool EnableSsl { get; set; } = true;
        public string FromAddress { get; set; } = string.Empty;
        public string FromDisplayName { get; set; } = "织绣品监测系统";
    }

    public class ServiceEndpointsConfig
    {
        public string ApiGateway { get; set; } = "http://localhost:5000";
        public string PopulationSimUrl { get; set; } = "http://localhost:5001";
        public string MildewGompertzUrl { get; set; } = "http://localhost:5002";
        public string AlertDispatchUrl { get; set; } = "http://localhost:5003";
        public string ZigBeeIngestUrl { get; set; } = "http://localhost:5004";
    }
}
