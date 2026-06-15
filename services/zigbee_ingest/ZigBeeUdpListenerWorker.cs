
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using TextileMonitoring.Contracts.Messages;
using ILogger = Serilog.ILogger;

namespace ZigBeeIngest.Worker;

public class ZigBeeUdpListenerWorker : BackgroundService
{
    private readonly UdpSettings _udpSettings;
    private readonly BatchingSettings _batchingSettings;
    private readonly IBus _bus;
    private readonly ILogger _logger;
    private readonly UdpClient _udpClient;
    private readonly ConcurrentQueue<SensorDataReceived> _pendingQueue = new();
    private readonly PeriodicTimer _flushTimer;
    private int _totalReceived;
    private int _totalPublished;
    private int _totalParseErrors;
    private int _consecutiveTimeouts;

    public ZigBeeUdpListenerWorker(
        IOptions<UdpSettings> udpSettings,
        IOptions<BatchingSettings> batchingSettings,
        IBus bus,
        ILogger logger)
    {
        _udpSettings = udpSettings.Value;
        _batchingSettings = batchingSettings.Value;
        _bus = bus;
        _logger = logger;

        _udpClient = new UdpClient(_udpSettings.ListenPort);
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket,
            SocketOptionName.ReceiveTimeout, _udpSettings.ReceiveTimeoutMs);
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket,
            SocketOptionName.ExclusiveAddressUse, true);

        if (_udpSettings.EnableMulticast && !string.IsNullOrWhiteSpace(_udpSettings.MulticastGroup))
        {
            try
            {
                _udpClient.JoinMulticastGroup(IPAddress.Parse(_udpSettings.MulticastGroup));
                _logger.Information("Joined multicast group: {MulticastGroup}", _udpSettings.MulticastGroup);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to join multicast group, continuing with unicast only");
            }
        }

        _flushTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_batchingSettings.FlushIntervalMs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("ZigBee UDP Listener starting - port: {Port}, batch: {BatchSize}, timeout: {Timeout}ms",
            _udpSettings.ListenPort, _batchingSettings.BatchSize, _udpSettings.ReceiveTimeoutMs);

        var listenTask = ReceiveLoopAsync(stoppingToken);
        var flushTask = FlushLoopAsync(stoppingToken);
        var metricsTask = MetricsLoopAsync(stoppingToken);

        await Task.WhenAll(listenTask, flushTask, metricsTask);

        _logger.Information("ZigBee UDP Listener stopped. Total: {Received} received, {Published} published, {Errors} parse errors",
            _totalReceived, _totalPublished, _totalParseErrors);
    }

    private async Task ReceiveLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                var result = await _udpClient.ReceiveAsync(stoppingToken);

                _consecutiveTimeouts = 0;
                Interlocked.Increment(ref _totalReceived);

                var payloadBytes = result.Buffer;
                var sensorData = ParseZigBeePayload(payloadBytes, result.RemoteEndPoint);

                if (sensorData != null)
                {
                    _pendingQueue.Enqueue(sensorData);

                    if (_pendingQueue.Count >= _batchingSettings.BatchSize)
                    {
                        await FlushBatchAsync(stoppingToken);
                    }
                }
                else
                {
                    Interlocked.Increment(ref _totalParseErrors);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                Interlocked.Increment(ref _consecutiveTimeouts);
                if (_consecutiveTimeouts % 10 == 0)
                {
                    _logger.Verbose("UDP receive timeout ({Count} consecutive), continuing...", _consecutiveTimeouts);
                }
                continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in UDP receive loop");
                await Task.Delay(100, stoppingToken);
            }
        }
    }

    private SensorDataReceived? ParseZigBeePayload(byte[] payloadBytes, IPEndPoint remoteEndpoint)
    {
        try
        {
            if (payloadBytes.Length < 8)
            {
                _logger.Verbose("Discarding short packet ({Length} bytes) from {Remote}",
                    payloadBytes.Length, remoteEndpoint);
                return null;
            }

            var payloadType = payloadBytes[7];

            if (payloadType == 0x01 || payloadType == 0x02)
            {
                return ParseBinaryPayload(payloadBytes, payloadType, remoteEndpoint);
            }

            var textPayload = Encoding.UTF8.GetString(payloadBytes).Trim();
            if (textPayload.StartsWith('{') && textPayload.EndsWith('}'))
            {
                return ParseJsonPayload(textPayload, remoteEndpoint);
            }

            _logger.Verbose("Unknown payload type {Type:X2} from {Remote}", payloadType, remoteEndpoint);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to parse payload from {Remote}", remoteEndpoint);
            return null;
        }
    }

    private SensorDataReceived? ParseBinaryPayload(byte[] payloadBytes, byte payloadType, IPEndPoint remoteEndpoint)
    {
        try
        {
            var sensorCode = $"ZIG-{BitConverter.ToUInt16(payloadBytes, 0):X4}";
            var textileId = BitConverter.ToInt32(payloadBytes, 2);
            var temperature = BitConverter.ToSingle(payloadBytes, 8);
            var humidity = BitConverter.ToSingle(payloadBytes, 12);
            var signalStrength = (short)(payloadBytes.Length > 16 ? payloadBytes[16] : -50);

            var sensorData = new SensorDataReceived
            {
                CorrelationId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                SensorCode = sensorCode,
                SensorType = payloadType == 0x01 ? "dust" : "fungi",
                TextileId = textileId,
                Temperature = Math.Round(temperature, 2),
                Humidity = Math.Round(humidity, 2),
                SignalStrength = signalStrength,
                RawPayload = Convert.ToBase64String(payloadBytes)
            };

            if (payloadType == 0x01 && payloadBytes.Length >= 24)
            {
                sensorData.FrassDensity = Math.Round(BitConverter.ToSingle(payloadBytes, 18), 4);
                sensorData.HoleCount = BitConverter.ToInt32(payloadBytes, 22);
                sensorData.PM2_5 = Math.Round(BitConverter.ToSingle(payloadBytes, 26), 2);
                sensorData.PM10 = Math.Round(BitConverter.ToSingle(payloadBytes, 30), 2);
            }
            else if (payloadType == 0x02 && payloadBytes.Length >= 24)
            {
                sensorData.SporeCount = Math.Round(BitConverter.ToSingle(payloadBytes, 18), 2);
                sensorData.FungiCFU = Math.Round(BitConverter.ToSingle(payloadBytes, 22), 2);
                var fungiTypeBytes = new byte[16];
                Array.Copy(payloadBytes, 26, fungiTypeBytes, 0, Math.Min(16, payloadBytes.Length - 26));
                sensorData.DominantFungiType = Encoding.ASCII.GetString(fungiTypeBytes).TrimEnd('\0');
            }

            return sensorData;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to parse binary payload (type {Type:X2}) from {Remote}",
                payloadType, remoteEndpoint);
            return null;
        }
    }

    private SensorDataReceived? ParseJsonPayload(string jsonPayload, IPEndPoint remoteEndpoint)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var jsonData = JsonSerializer.Deserialize<JsonSensorPayload>(jsonPayload, options);

            if (jsonData == null || string.IsNullOrWhiteSpace(jsonData.SensorCode))
            {
                return null;
            }

            return new SensorDataReceived
            {
                CorrelationId = Guid.NewGuid(),
                Timestamp = jsonData.Timestamp ?? DateTime.UtcNow,
                SensorCode = jsonData.SensorCode,
                SensorType = jsonData.SensorType?.ToLowerInvariant() ?? "unknown",
                TextileId = jsonData.TextileId,
                Temperature = Math.Round(jsonData.Temperature, 2),
                Humidity = Math.Round(jsonData.Humidity, 2),
                FrassDensity = jsonData.FrassDensity.HasValue ? Math.Round(jsonData.FrassDensity.Value, 4) : null,
                HoleCount = jsonData.HoleCount,
                PM2_5 = jsonData.PM2_5.HasValue ? Math.Round(jsonData.PM2_5.Value, 2) : null,
                PM10 = jsonData.PM10.HasValue ? Math.Round(jsonData.PM10.Value, 2) : null,
                SporeCount = jsonData.SporeCount.HasValue ? Math.Round(jsonData.SporeCount.Value, 2) : null,
                FungiCFU = jsonData.FungiCFU.HasValue ? Math.Round(jsonData.FungiCFU.Value, 2) : null,
                DominantFungiType = jsonData.DominantFungiType,
                SignalStrength = jsonData.SignalStrength,
                RawPayload = jsonPayload
            };
        }
        catch (JsonException ex)
        {
            _logger.Warning(ex, "Failed to parse JSON payload from {Remote}", remoteEndpoint);
            return null;
        }
    }

    private async Task FlushLoopAsync(CancellationToken stoppingToken)
    {
        while (await _flushTimer.WaitForNextTickAsync(stoppingToken))
        {
            if (_pendingQueue.Count > 0)
            {
                _logger.Verbose("Flush timer triggered, queue depth: {Count}", _pendingQueue.Count);
                await FlushBatchAsync(stoppingToken);
            }
        }
    }

    private async Task MetricsLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _logger.Information("ZigBee UDP metrics - Received: {Received}, Published: {Published}, " +
                "Queue: {QueueDepth}, ParseErrors: {Errors}, ConsecutiveTimeouts: {Timeouts}",
                _totalReceived, _totalPublished, _pendingQueue.Count, _totalParseErrors, _consecutiveTimeouts);
        }
    }

    private async Task FlushBatchAsync(CancellationToken stoppingToken)
    {
        var batch = new List<SensorDataReceived>(_batchingSettings.BatchSize);

        while (_pendingQueue.TryDequeue(out var item) && batch.Count < _batchingSettings.BatchSize)
        {
            batch.Add(item);
        }

        if (batch.Count == 0) return;

        try
        {
            var publishTasks = batch.Select(x => _bus.Publish(x, stoppingToken));
            await Task.WhenAll(publishTasks);

            Interlocked.Add(ref _totalPublished, batch.Count);
            _logger.Verbose("Published batch of {Count} sensor data events to exchange", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to publish batch of {Count} events. Re-queuing...", batch.Count);

            foreach (var item in Enumerable.Reverse(batch))
            {
                _pendingQueue.Enqueue(item);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _flushTimer.Dispose();
        try
        {
            if (_udpSettings.EnableMulticast && !string.IsNullOrWhiteSpace(_udpSettings.MulticastGroup))
            {
                _udpClient.DropMulticastGroup(IPAddress.Parse(_udpSettings.MulticastGroup));
            }
        }
        catch
        {
        }
        _udpClient?.Close();
        _udpClient?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private class JsonSensorPayload
    {
        public string? SensorCode { get; set; }
        public string? SensorType { get; set; }
        public int TextileId { get; set; }
        public DateTime? Timestamp { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double? FrassDensity { get; set; }
        public int? HoleCount { get; set; }
        public double? PM2_5 { get; set; }
        public double? PM10 { get; set; }
        public double? SporeCount { get; set; }
        public double? FungiCFU { get; set; }
        public string? DominantFungiType { get; set; }
        public short SignalStrength { get; set; }
    }
}
