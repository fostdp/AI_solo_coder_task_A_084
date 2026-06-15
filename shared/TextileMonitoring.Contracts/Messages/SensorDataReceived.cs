
namespace TextileMonitoring.Contracts.Messages;

public record SensorDataReceived
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string SensorCode { get; init; } = string.Empty;
    public string SensorType { get; init; } = string.Empty;
    public int TextileId { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }

    public double? FrassDensity { get; init; }
    public int? HoleCount { get; init; }
    public double? PM2_5 { get; init; }
    public double? PM10 { get; init; }

    public double? SporeCount { get; init; }
    public double? FungiCFU { get; init; }
    public string? DominantFungiType { get; init; }

    public short SignalStrength { get; init; }
    public string? RawPayload { get; init; }
}
