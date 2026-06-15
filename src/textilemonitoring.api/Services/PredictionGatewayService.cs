
using MassTransit;
using TextileMonitoring.Contracts.Messages;
using TextileMonitoring.Contracts.RabbitMQ;
using TextileMonitoring.Data.Repositories;

namespace TextileMonitoring.API.Services;

public interface IPredictionGatewayService
{
    Task<PopulationPredictionGenerated?> GetPopulationPredictionAsync(int textileId, int horizonDays = 30, CancellationToken ct = default);
    Task<MildewPredictionGenerated?> GetMildewPredictionAsync(int textileId, int horizonDays = 30, CancellationToken ct = default);
    Task<(PopulationPredictionGenerated?, MildewPredictionGenerated?)> GetCombinedPredictionAsync(
        int textileId, int horizonDays = 30, CancellationToken ct = default);
    Task PublishSensorDataAsync(SensorDataReceived data, CancellationToken ct = default);
    Task<AlertTriggered?> TriggerAlertAsync(AlertTriggered alert, CancellationToken ct = default);
}

public class PredictionGatewayService : IPredictionGatewayService
{
    private readonly IBus _bus;
    private readonly ITextileRepository _textileRepository;
    private readonly ILogger<PredictionGatewayService> _logger;
    private readonly IRequestClient<SensorDataReceived, PopulationPredictionGenerated> _popClient;
    private readonly IRequestClient<SensorDataReceived, MildewPredictionGenerated> _mildewClient;

    public PredictionGatewayService(
        IBus bus,
        ITextileRepository textileRepository,
        ILogger<PredictionGatewayService> logger,
        IRequestClient<SensorDataReceived, PopulationPredictionGenerated> popClient,
        IRequestClient<SensorDataReceived, MildewPredictionGenerated> mildewClient)
    {
        _bus = bus;
        _textileRepository = textileRepository;
        _logger = logger;
        _popClient = popClient;
        _mildewClient = mildewClient;
    }

    public async Task<PopulationPredictionGenerated?> GetPopulationPredictionAsync(
        int textileId, int horizonDays = 30, CancellationToken ct = default)
    {
        var textile = await _textileRepository.GetByIdAsync(textileId, ct);
        if (textile == null) return null;

        var request = BuildSensorRequest(textileId, textile);
        try
        {
            var response = await _popClient.GetResponse<PopulationPredictionGenerated>(request, ct);
            return response.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "种群预测请求失败 TextileId={TextileId}", textileId);
            return null;
        }
    }

    public async Task<MildewPredictionGenerated?> GetMildewPredictionAsync(
        int textileId, int horizonDays = 30, CancellationToken ct = default)
    {
        var textile = await _textileRepository.GetByIdAsync(textileId, ct);
        if (textile == null) return null;

        var request = BuildSensorRequest(textileId, textile);
        try
        {
            var response = await _mildewClient.GetResponse<MildewPredictionGenerated>(request, ct);
            return response.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "霉菌预测请求失败 TextileId={TextileId}", textileId);
            return null;
        }
    }

    public async Task<(PopulationPredictionGenerated?, MildewPredictionGenerated?)> GetCombinedPredictionAsync(
        int textileId, int horizonDays = 30, CancellationToken ct = default)
    {
        var textile = await _textileRepository.GetByIdAsync(textileId, ct);
        if (textile == null) return (null, null);

        var request = BuildSensorRequest(textileId, textile);

        var popTask = _popClient.GetResponse<PopulationPredictionGenerated>(request, ct);
        var mildewTask = _mildewClient.GetResponse<MildewPredictionGenerated>(request, ct);

        await Task.WhenAll(popTask, mildewTask);

        PopulationPredictionGenerated? popResult = null;
        MildewPredictionGenerated? mildewResult = null;

        try { popResult = popTask.Result.Message; }
        catch (Exception ex) { _logger.LogError(ex, "种群预测失败"); }

        try { mildewResult = mildewTask.Result.Message; }
        catch (Exception ex) { _logger.LogError(ex, "霉菌预测失败"); }

        return (popResult, mildewResult);
    }

    public async Task PublishSensorDataAsync(SensorDataReceived data, CancellationToken ct = default)
    {
        await _bus.Publish(data, ct);
        _logger.LogDebug("传感器数据已发布 CorrelationId={CorrelationId}", data.CorrelationId);
    }

    public async Task<AlertTriggered?> TriggerAlertAsync(AlertTriggered alert, CancellationToken ct = default)
    {
        await _bus.Publish(alert, ct);
        return alert;
    }

    private static SensorDataReceived BuildSensorRequest(int textileId, TextileMonitoring.Data.Entities.Textile textile)
    {
        var latestHole = textile.HoleMarkers
            .OrderByDescending(h => h.DetectedAt)
            .FirstOrDefault();
        var latestFungi = textile.MoldRegions
            .OrderByDescending(m => m.DetectedAt)
            .FirstOrDefault();

        return new SensorDataReceived
        {
            CorrelationId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            SensorCode = $"GATEWAY-{textileId}",
            SensorType = "Gateway",
            TextileId = textileId,
            Temperature = 22.5,
            Humidity = 55.0,
            FrassDensity = (double?)latestHole?.SeverityLevel * 0.01 ?? 0.05,
            HoleCount = textile.HoleMarkers.Count,
            PM2_5 = 35.0,
            PM10 = 60.0,
            SporeCount = latestFungi != null ? (double)latestFungi.SeverityLevel * 100 : 120.0,
            FungiCFU = latestFungi != null ? (double)latestFungi.SeverityLevel * 75 : 100.0,
            DominantFungiType = latestFungi?.DominantFungiType ?? "Aspergillus",
            SignalStrength = 0
        };
    }
}
