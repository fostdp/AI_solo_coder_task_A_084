
namespace TextileMonitoring.Data.Entities;

public class Textile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Dynasty { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal AreaCm2 { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime? AcquisitionDate { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Sensor> Sensors { get; set; } = new List<Sensor>();
    public ICollection<HoleMarker> HoleMarkers { get; set; } = new List<HoleMarker>();
    public ICollection<MoldRegion> MoldRegions { get; set; } = new List<MoldRegion>();
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}

public enum SensorType
{
    DustSensor = 1,
    FungiSensor = 2
}

public class Sensor
{
    public int Id { get; set; }
    public int TextileId { get; set; }
    public string SensorCode { get; set; } = string.Empty;
    public SensorType SensorType { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }

    public Textile? Textile { get; set; }
}

public class DustSensorData
{
    public long Id { get; set; }
    public int SensorId { get; set; }
    public int TextileId { get; set; }
    public DateTime ReadingTime { get; set; }
    public decimal PM2_5 { get; set; }
    public decimal PM10 { get; set; }
    public decimal FrassDensity { get; set; }
    public int HoleCount { get; set; }
    public decimal HoleDensity { get; set; }
    public decimal? Temperature { get; set; }
    public decimal? Humidity { get; set; }
    public short? ZigBeeSignalStrength { get; set; }
    public int SensorStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FungiSensorData
{
    public long Id { get; set; }
    public int SensorId { get; set; }
    public int TextileId { get; set; }
    public DateTime ReadingTime { get; set; }
    public decimal SporeCount { get; set; }
    public decimal FungiCFU { get; set; }
    public decimal? Temperature { get; set; }
    public decimal? Humidity { get; set; }
    public string? DominantFungiType { get; set; }
    public short? ZigBeeSignalStrength { get; set; }
    public int SensorStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class HoleMarker
{
    public int Id { get; set; }
    public int TextileId { get; set; }
    public int SensorId { get; set; }
    public int? ImageId { get; set; }
    public decimal RelativeX { get; set; }
    public decimal RelativeY { get; set; }
    public decimal RadiusMm { get; set; }
    public decimal? PerimeterMm { get; set; }
    public decimal? AreaMm2 { get; set; }
    public int SeverityLevel { get; set; }
    public DateTime DetectedAt { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public Textile? Textile { get; set; }
}

public class MoldRegion
{
    public int Id { get; set; }
    public int TextileId { get; set; }
    public int? SensorId { get; set; }
    public int? ImageId { get; set; }
    public decimal RelativeX { get; set; }
    public decimal RelativeY { get; set; }
    public decimal RadiusMm { get; set; }
    public decimal? AreaMm2 { get; set; }
    public string? DominantFungiType { get; set; }
    public int SeverityLevel { get; set; }
    public DateTime DetectedAt { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public Textile? Textile { get; set; }
}

public enum PredictionModel
{
    Logistic = 1,
    Gompertz = 2,
    Synergy = 3,
    LotkaVolterra = 4
}

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public class Prediction
{
    public long Id { get; set; }
    public int TextileId { get; set; }
    public PredictionModel Model { get; set; }
    public int HorizonDays { get; set; }
    public DateTime PredictionDate { get; set; }
    public decimal MaxPredictedValue { get; set; }
    public decimal? PredictedFungiCFU { get; set; }
    public decimal? SynergyRisk { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public double Confidence { get; set; }
    public string? ParametersJson { get; set; }
    public string? PredictionJson { get; set; }
    public DateTime CreatedAt { get; set; }

    public Textile? Textile { get; set; }
}

public class Alert
{
    public int Id { get; set; }
    public int TextileId { get; set; }
    public int AlertType { get; set; }
    public int AlertLevel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal ActualValue { get; set; }
    public decimal Threshold { get; set; }
    public bool Resolved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }

    public Textile? Textile { get; set; }
}

public class AlertConfig
{
    public int Id { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
