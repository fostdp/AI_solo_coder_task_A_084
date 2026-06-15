
namespace TextileMonitoring.Contracts.Messages;

public record MildewPredictionGenerated
{
    public Guid CorrelationId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int TextileId { get; init; }
    public int HorizonDays { get; init; }
    public string ModelType { get; init; } = "Gompertz";

    public double InitialCFU { get; init; }
    public double CarryingCapacityK { get; init; }
    public double GrowthRateRho { get; init; }

    public double AvgTemperature { get; init; }
    public double AvgHumidity { get; init; }

    public List<MildewPoint> PredictionPoints { get; init; } = new();

    public double FinalCFU { get; init; }
    public double MaxCFU { get; init; }
    public string? DominantFungiType { get; init; }
    public string? RiskLevel { get; init; }
    public double Confidence { get; init; }

    public double InflectionPointDay { get; init; }
    public double DoublingTimeHours { get; init; }
}

public record MildewPoint
{
    public int Day { get; init; }
    public double FungiCFU { get; init; }
    public double GrowthRate { get; init; }
    public double CumulativeSporeCount { get; init; }
}
