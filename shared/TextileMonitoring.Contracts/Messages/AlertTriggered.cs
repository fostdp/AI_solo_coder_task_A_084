
namespace TextileMonitoring.Contracts.Messages;

public record AlertTriggered
{
    public Guid CorrelationId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int TextileId { get; init; }
    public string TextileName { get; init; } = string.Empty;

    public string AlertType { get; init; } = string.Empty;
    public string AlertLevel { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public double ActualValue { get; init; }
    public double Threshold { get; init; }

    public string? SourcePredictionId { get; init; }
    public string? Recommendation { get; init; }

    public Dictionary<string, object> Metadata { get; init; } = new();
}

public record AlertDispatched
{
    public Guid CorrelationId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int AlertId { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string Recipient { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
