using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PopulationSim.Service.Models;
using PopulationSim.Service.Services;
using TextileMonitoring.Contracts.Messages;
using TextileMonitoring.Contracts.RabbitMQ;
using TextileMonitoring.Data;

namespace PopulationSim.Service.Consumers;

public class SensorDataConsumer : IConsumer<SensorDataReceived>
{
    private readonly PredictionWindowManager _windowManager;
    private readonly LotkaVolterraPredictionService _predictionService;
    private readonly TextileMonitoringDbContext _dbContext;
    private readonly PopulationModelConfig _modelConfig;
    private readonly ILogger<SensorDataConsumer> _logger;

    public SensorDataConsumer(
        PredictionWindowManager windowManager,
        LotkaVolterraPredictionService predictionService,
        TextileMonitoringDbContext dbContext,
        IOptions<PopulationModelConfig> modelConfig,
        ILogger<SensorDataConsumer> logger)
    {
        _windowManager = windowManager;
        _predictionService = predictionService;
        _dbContext = dbContext;
        _modelConfig = modelConfig.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SensorDataReceived> context)
    {
        var message = context.Message;

        _logger.Debug(
            "Received SensorData: TextileId={TextileId}, Sensor={SensorCode}, T={Temperature}, H={Humidity}",
            message.TextileId, message.SensorCode, message.Temperature, message.Humidity);

        try
        {
            var result = _windowManager.AddSensorData(message);

            if (result.ShouldProcess && result.WindowData != null)
            {
                await ProcessWindowAsync(
                    message.TextileId,
                    result.WindowData,
                    message.CorrelationId,
                    context);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Error processing sensor data for TextileId: {TextileId}, CorrelationId: {CorrelationId}",
                message.TextileId,
                message.CorrelationId);
            throw;
        }
    }

    private async Task ProcessWindowAsync(
        int textileId,
        List<SensorDataReceived> windowData,
        Guid correlationId,
        ConsumeContext context)
    {
        _logger.Information(
            "Processing window for TextileId: {TextileId}, {Count} data points",
            textileId,
            windowData.Count);

        var textile = await _dbContext.Textiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == textileId);

        if (textile == null)
        {
            _logger.Warning("Textile not found: {TextileId}", textileId);
            return;
        }

        var (avgTemperature, avgHumidity, initialPestDensity) = AggregateWindowData(windowData, textile);

        var prediction = _predictionService.SolvePrediction(
            textileId: textileId,
            avgTemperature: avgTemperature,
            avgHumidity: avgHumidity,
            initialPestDensity: initialPestDensity,
            initialPredatorDensity: _modelConfig.DefaultInitialPredatorDensity,
            horizonDays: _modelConfig.DefaultHorizonDays,
            correlationId: correlationId);

        await context.Publish(prediction, c =>
        {
            c.SetRoutingKey(QueueNames.RoutingKeys.Population);
        });

        _logger.Information(
            "Published PopulationPrediction: TextileId={TextileId}, Risk={RiskLevel}, Confidence={Confidence}, MaxPest={MaxPest}",
            prediction.TextileId,
            prediction.RiskLevel,
            prediction.Confidence,
            prediction.MaxPestDensity);
    }

    private (double AvgTemperature, double AvgHumidity, double InitialPestDensity) AggregateWindowData(
        List<SensorDataReceived> data,
        TextileMonitoring.Data.Entities.Textile textile)
    {
        var validTemps = data.Where(d => d.Temperature > -40 && d.Temperature < 80)
                            .Select(d => d.Temperature)
                            .DefaultIfEmpty(_modelConfig.TemperatureOptimal)
                            .ToList();

        var validHums = data.Where(d => d.Humidity >= 0 && d.Humidity <= 100)
                            .Select(d => d.Humidity)
                            .DefaultIfEmpty(_modelConfig.HumidityOptimal)
                            .ToList();

        double avgTemp = validTemps.Average();
        double avgHum = validHums.Average();

        double pestDensity = EstimatePestDensity(data, textile);

        _logger.Debug(
            "Window aggregation: T={AvgTemp}, H={AvgHum}, PestDensity={PestDensity}",
            Math.Round(avgTemp, 2),
            Math.Round(avgHum, 2),
            Math.Round(pestDensity, 4));

        return (avgTemp, avgHum, pestDensity);
    }

    private double EstimatePestDensity(List<SensorDataReceived> data, TextileMonitoring.Data.Entities.Textile textile)
    {
        double density = _modelConfig.DefaultInitialPestDensity;
        bool hasMeasurement = false;

        var frassData = data.Where(d => d.FrassDensity.HasValue && d.FrassDensity.Value > 0)
                            .Select(d => d.FrassDensity.Value)
                            .ToList();

        if (frassData.Any())
        {
            double avgFrass = frassData.Average();
            density = Math.Max(density, avgFrass * 0.5);
            hasMeasurement = true;
        }

        var holeData = data.Where(d => d.HoleCount.HasValue && d.HoleCount.Value > 0)
                          .Select(d => d.HoleCount.Value)
                          .ToList();

        if (holeData.Any() && textile.AreaCm2 > 0)
        {
            double avgHoles = holeData.Average();
            double holeDensity = avgHoles / (double)textile.AreaCm2 * 100;
            density = Math.Max(density, holeDensity);
            hasMeasurement = true;
        }

        var pm25Data = data.Where(d => d.PM2_5.HasValue && d.PM2_5.Value > 0)
                          .Select(d => d.PM2_5.Value)
                          .ToList();

        if (pm25Data.Any())
        {
            double avgPm25 = pm25Data.Average();
            double pmEstimate = Math.Min(10.0, avgPm25 * 0.02);
            density = Math.Max(density, pmEstimate);
            hasMeasurement = true;
        }

        if (!hasMeasurement)
        {
            _logger.Debug("No direct pest measurements, using default density: {Density}", density);
        }

        return Math.Max(0.01, Math.Min(density, _modelConfig.DefaultCarryingCapacity * 0.8));
    }
}
