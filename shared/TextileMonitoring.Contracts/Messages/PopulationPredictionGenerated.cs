
namespace TextileMonitoring.Contracts.Messages;

public record PopulationPredictionGenerated
{
    public Guid CorrelationId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int TextileId { get; init; }
    public int HorizonDays { get; init; }
    public string ModelType { get; init; } = "LotkaVolterra";

    public double InitialPestDensity { get; init; }
    public double InitialPredatorDensity { get; init; }

    public double AvgTemperature { get; init; }
    public double AvgHumidity { get; init; }

    public List<PredictionPoint> PredictionPoints { get; init; } = new();

    public double MaxPestDensity { get; init; }
    public double MaxPredatorDensity { get; init; }
    public double FinalPestDensity { get; init; }
    public double FinalPredatorDensity { get; init; }

    public string? RiskLevel { get; init; }
    public double Confidence { get; init; }

    public double AlphaPredation { get; init; }
    public double BetaConversion { get; init; }
    public double DeltaMortality { get; init; }
}

public record PredictionPoint
{
    public int Day { get; init; }
    public double PestDensity { get; init; }
    public double PredatorDensity { get; init; }
    public double PredationRate { get; init; }
    public double NetGrowthRate { get; init; }
}
