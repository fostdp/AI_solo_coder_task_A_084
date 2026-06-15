
namespace TextileMonitoring.Messages.Events
{
    public interface ISensorDataReceived
    {
        Guid CorrelationId { get; }
        DateTime Timestamp { get; }
        int TextileId { get; }
        string SensorCode { get; }
        SensorType SensorType { get; }
        decimal? Temperature { get; }
        decimal? Humidity { get; }
        decimal? PM2_5 { get; }
        decimal? PM10 { get; }
        decimal? FrassDensity { get; }
        int? HoleCount { get; }
        decimal? HoleDensity { get; }
        double? SporeCount { get; }
        decimal? FungiCFU { get; }
        string? DominantFungiType { get; }
        short? SignalStrength { get; }
    }

    public enum SensorType
    {
        DustSensor = 1,
        FungiSensor = 2
    }

    public class SensorDataReceived : ISensorDataReceived
    {
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TextileId { get; set; }
        public string SensorCode { get; set; } = string.Empty;
        public SensorType SensorType { get; set; }
        public decimal? Temperature { get; set; }
        public decimal? Humidity { get; set; }
        public decimal? PM2_5 { get; set; }
        public decimal? PM10 { get; set; }
        public decimal? FrassDensity { get; set; }
        public int? HoleCount { get; set; }
        public decimal? HoleDensity { get; set; }
        public double? SporeCount { get; set; }
        public decimal? FungiCFU { get; set; }
        public string? DominantFungiType { get; set; }
        public short? SignalStrength { get; set; }
    }

    public class PredictionCalculated
    {
        public Guid CorrelationId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TextileId { get; set; }
        public string TextileName { get; set; } = string.Empty;
        public int HorizonDays { get; set; }
        public string Model { get; set; } = "LotkaVolterra";
        public decimal MaxPredictedHoleDensity { get; set; }
        public double PredatorDensity { get; set; }
        public double PredationRate { get; set; }
        public int RiskLevel { get; set; }
        public double Confidence { get; set; }
        public decimal Temperature { get; set; }
        public decimal Humidity { get; set; }
        public List<PredictionPoint> DataPoints { get; set; } = new();
    }

    public class PredictionPoint
    {
        public DateTime Date { get; set; }
        public decimal PredictedHoleDensity { get; set; }
        public double PredatorDensity { get; set; }
        public double PredationRate { get; set; }
    }

    public class MildewAnalysisCompleted
    {
        public Guid CorrelationId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TextileId { get; set; }
        public string TextileName { get; set; } = string.Empty;
        public int HorizonDays { get; set; }
        public string Model { get; set; } = "Gompertz";
        public decimal MaxPredictedFungiCFU { get; set; }
        public int RiskLevel { get; set; }
        public double Confidence { get; set; }
        public decimal Temperature { get; set; }
        public decimal Humidity { get; set; }
        public string? DominantFungiType { get; set; }
        public List<MildewPredictionPoint> DataPoints { get; set; } = new();
        public List<MoldRegionInfo> MoldRegions { get; set; } = new();
    }

    public class MildewPredictionPoint
    {
        public DateTime Date { get; set; }
        public decimal PredictedFungiCFU { get; set; }
    }

    public class MoldRegionInfo
    {
        public int Id { get; set; }
        public decimal RelativeX { get; set; }
        public decimal RelativeY { get; set; }
        public decimal RadiusMm { get; set; }
        public string DominantFungiType { get; set; } = string.Empty;
    }

    public class SynergyRiskEvaluated
    {
        public Guid CorrelationId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TextileId { get; set; }
        public decimal HoleDensity { get; set; }
        public decimal FungiCFU { get; set; }
        public decimal SynergyRisk { get; set; }
        public int RiskLevel { get; set; }
    }

    public class AlertDispatched
    {
        public Guid CorrelationId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TextileId { get; set; }
        public int AlertId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public int AlertLevel { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal ActualValue { get; set; }
        public decimal Threshold { get; set; }
        public bool DingTalkSent { get; set; }
        public bool EmailSent { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

namespace TextileMonitoring.Messages.Commands
{
    public class CalculatePrediction
    {
        public Guid CorrelationId { get; set; }
        public int TextileId { get; set; }
        public int HorizonDays { get; set; } = 30;
        public string? TriggerSource { get; set; }
    }

    public class AnalyzeMildew
    {
        public Guid CorrelationId { get; set; }
        public int TextileId { get; set; }
        public int HorizonDays { get; set; } = 30;
        public string? TriggerSource { get; set; }
    }

    public class DispatchAlert
    {
        public Guid CorrelationId { get; set; }
        public int AlertId { get; set; }
        public int TextileId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public int AlertLevel { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal ActualValue { get; set; }
        public decimal Threshold { get; set; }
    }
}
