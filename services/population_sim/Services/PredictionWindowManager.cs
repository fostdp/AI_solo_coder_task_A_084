using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PopulationSim.Service.Models;
using TextileMonitoring.Contracts.Messages;

namespace PopulationSim.Service.Services;

public class PredictionWindowManager
{
    private readonly ConcurrentDictionary<int, TextileWindowState> _textileWindows = new();
    private readonly PredictionWindowConfig _windowConfig;
    private readonly ILogger<PredictionWindowManager> _logger;

    public PredictionWindowManager(
        IOptions<PredictionWindowConfig> windowConfig,
        ILogger<PredictionWindowManager> logger)
    {
        _windowConfig = windowConfig.Value;
        _logger = logger;
    }

    public WindowAddResult AddSensorData(SensorDataReceived data)
    {
        var result = new WindowAddResult();
        var state = _textileWindows.GetOrAdd(data.TextileId, _ => new TextileWindowState
        {
            TextileId = data.TextileId,
            WindowStart = DateTime.UtcNow,
            SensorData = new List<SensorDataReceived>()
        });

        lock (state)
        {
            state.SensorData.Add(data);

            var windowAge = DateTime.UtcNow - state.WindowStart;
            var hasEnoughData = state.SensorData.Count >= _windowConfig.MinDataPoints;
            var windowExpired = windowAge >= TimeSpan.FromMinutes(_windowConfig.WindowMinutes);
            var batchFull = state.SensorData.Count >= _windowConfig.MaxBatchSize;

            if ((windowExpired || batchFull) && hasEnoughData)
            {
                result.ShouldProcess = true;
                result.WindowData = state.SensorData.ToList();
                result.WindowDuration = windowAge;

                state.SensorData.Clear();
                state.WindowStart = DateTime.UtcNow;

                _logger.Information(
                    "Window closed for TextileId: {TextileId}, DataPoints: {Count}, Duration: {Duration}s, Reason: {Reason}",
                    data.TextileId,
                    result.WindowData.Count,
                    windowAge.TotalSeconds,
                    windowExpired ? "Timeout" : "BatchFull");
            }
            else
            {
                _logger.Debug(
                    "Added data to window TextileId: {TextileId}, Count: {Count}/{Min}, Age: {Age}s",
                    data.TextileId,
                    state.SensorData.Count,
                    _windowConfig.MinDataPoints,
                    windowAge.TotalSeconds);
            }
        }

        return result;
    }

    public void ForceProcessAllWindows(Func<int, List<SensorDataReceived>, Task> processor)
    {
        foreach (var kvp in _textileWindows)
        {
            var state = kvp.Value;
            lock (state)
            {
                if (state.SensorData.Count >= _windowConfig.MinDataPoints)
                {
                    _logger.Information(
                        "Force processing window for TextileId: {TextileId}, DataPoints: {Count}",
                        state.TextileId,
                        state.SensorData.Count);

                    var data = state.SensorData.ToList();
                    state.SensorData.Clear();
                    state.WindowStart = DateTime.UtcNow;

                    processor(state.TextileId, data).GetAwaiter().GetResult();
                }
            }
        }
    }

    public IEnumerable<(int TextileId, int DataCount, TimeSpan WindowAge)> GetWindowStatus()
    {
        foreach (var kvp in _textileWindows)
        {
            var state = kvp.Value;
            lock (state)
            {
                yield return (
                    state.TextileId,
                    state.SensorData.Count,
                    DateTime.UtcNow - state.WindowStart);
            }
        }
    }
}

public class TextileWindowState
{
    public int TextileId { get; set; }
    public DateTime WindowStart { get; set; }
    public List<SensorDataReceived> SensorData { get; set; } = new();
}

public class WindowAddResult
{
    public bool ShouldProcess { get; set; }
    public List<SensorDataReceived>? WindowData { get; set; }
    public TimeSpan WindowDuration { get; set; }
}
