
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TextileMonitoring.Infrastructure.Configuration;
using TextileMonitoring.Infrastructure.Messaging;
using TextileMonitoring.Messages.Events;
using Serilog;
using ILogger = Serilog.ILogger;

namespace TextileMonitoring.ZigBeeIngest
{
    public class ZigBeeListenerWorker : BackgroundService
    {
        private readonly ZigBeeConfig _config;
        private readonly ISensorDataAdapter _adapter;
        private readonly IMessageBus _bus;
        private readonly ILogger _logger;
        private readonly UdpClient _udpClient;
        private readonly ConcurrentQueue<ISensorDataReceived> _pendingQueue = new();
        private readonly PeriodicTimer _flushTimer;
        private int _totalReceived;
        private int _totalPublished;

        public ZigBeeListenerWorker(
            IOptions<ZigBeeConfig> config,
            ISensorDataAdapter adapter,
            IMessageBus bus,
            ILogger logger)
        {
            _config = config.Value;
            _adapter = adapter;
            _bus = bus;
            _logger = logger;
            _udpClient = new UdpClient(_config.ListenPort);
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket,
                SocketOptionName.ReceiveTimeout, _config.ReceiveTimeoutMs);
            _flushTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_config.FlushIntervalMs));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Information("ZigBee Ingest Service starting - port: {Port}, batch: {BatchSize}",
                _config.ListenPort, _config.BatchSize);

            var listenTask = ReceiveLoopAsync(stoppingToken);
            var flushTask = FlushLoopAsync(stoppingToken);
            var metricsTask = MetricsLoopAsync(stoppingToken);

            await Task.WhenAll(listenTask, flushTask, metricsTask);

            _logger.Information("ZigBee Ingest Service stopped. Total: {Received} received, {Published} published",
                _totalReceived, _totalPublished);
        }

        private async Task ReceiveLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                    var result = await _udpClient.ReceiveAsync(stoppingToken);

                    Interlocked.Increment(ref _totalReceived);
                    var payloadBytes = result.Buffer;

                    if (payloadBytes.Length < 8)
                    {
                        _logger.Verbose("Discarding short packet ({Length} bytes) from {Remote}",
                            payloadBytes.Length, result.RemoteEndPoint);
                        continue;
                    }

                    var payloadType = payloadBytes[7];
                    ISensorDataReceived? sensorData = payloadType switch
                    {
                        0x01 => _adapter.ParseDustPayload(payloadBytes, result.RemoteEndPoint),
                        0x02 => _adapter.ParseFungiPayload(payloadBytes, result.RemoteEndPoint),
                        _ => null
                    };

                    if (sensorData != null)
                    {
                        _pendingQueue.Enqueue(sensorData);

                        if (_pendingQueue.Count >= _config.BatchSize)
                        {
                            await FlushBatchAsync(stoppingToken);
                        }
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    continue;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in receive loop");
                    await Task.Delay(100, stoppingToken);
                }
            }
        }

        private async Task FlushLoopAsync(CancellationToken stoppingToken)
        {
            while (await _flushTimer.WaitForNextTickAsync(stoppingToken))
            {
                if (_pendingQueue.Count > 0)
                {
                    await FlushBatchAsync(stoppingToken);
                }
            }
        }

        private async Task MetricsLoopAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.Information("ZigBee metrics - Received: {Received}, Published: {Published}, Queue: {QueueDepth}",
                    _totalReceived, _totalPublished, _pendingQueue.Count);
            }
        }

        private async Task FlushBatchAsync(CancellationToken stoppingToken)
        {
            var batch = new List<ISensorDataReceived>(_config.BatchSize);

            while (_pendingQueue.TryDequeue(out var item) && batch.Count < _config.BatchSize)
            {
                batch.Add(item);
            }

            if (batch.Count == 0) return;

            try
            {
                var tasks = batch.Select(x => _bus.PublishAsync((dynamic)x, stoppingToken));
                await Task.WhenAll(tasks);

                Interlocked.Add(ref _totalPublished, batch.Count);
                _logger.Verbose("Published batch of {Count} sensor data events", batch.Count);
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
            _udpClient?.Close();
            _udpClient?.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
