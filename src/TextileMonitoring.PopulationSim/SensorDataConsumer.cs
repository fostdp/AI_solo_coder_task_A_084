using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using TextileMonitoring.Infrastructure.Configuration;
using TextileMonitoring.Messages.Events;
using TextileMonitoring.PopulationSim.Data;
using TextileMonitoring.PopulationSim.Models;

namespace TextileMonitoring.PopulationSim
{
    public class SensorDataConsumer : IConsumer<ISensorDataReceived>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly LotkaVolterraPredictionService _predictionService;
        private readonly PopulationModelConfig _populationConfig;
        private readonly ILogger _logger;

        public SensorDataConsumer(
            ApplicationDbContext dbContext,
            LotkaVolterraPredictionService predictionService,
            IOptions<PopulationModelConfig> populationConfig,
            ILogger logger)
        {
            _dbContext = dbContext;
            _predictionService = predictionService;
            _populationConfig = populationConfig.Value;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ISensorDataReceived> context)
        {
            var message = context.Message;

            if (message.SensorType != SensorType.DustSensor)
            {
                _logger.Verbose("Skipping non-dust sensor data: {SensorType}, TextileId: {TextileId}",
                    message.SensorType, message.TextileId);
                return;
            }

            _logger.Information("Processing dust sensor data for TextileId: {TextileId}, CorrelationId: {CorrelationId}",
                message.TextileId, message.CorrelationId);

            try
            {
                var textile = await _dbContext.Textiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == message.TextileId);

                if (textile == null)
                {
                    _logger.Warning("Textile not found: {TextileId}", message.TextileId);
                    return;
                }

                var historicalData = await GetHistoricalData(message.TextileId);
                var initialConditions = ExtractInitialConditions(message, historicalData, textile);

                var prediction = _predictionService.SolvePrediction(
                    textileId: message.TextileId,
                    textileName: textile.Name,
                    temperature: initialConditions.Temperature,
                    humidity: initialConditions.Humidity,
                    initialPestDensity: initialConditions.PestDensity,
                    initialPredatorDensity: _populationConfig.DefaultInitialPredatorDensity,
                    initialFungiCFU: initialConditions.FungiCFU,
                    horizonDays: _populationConfig.DefaultHorizonDays);

                prediction.CorrelationId = message.CorrelationId;

                await context.Publish(prediction);

                _logger.Information("Published prediction for TextileId: {TextileId}, Horizon: {HorizonDays} days, RiskLevel: {RiskLevel}",
                    message.TextileId, prediction.HorizonDays, prediction.RiskLevel);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing sensor data for TextileId: {TextileId}, CorrelationId: {CorrelationId}",
                    message.TextileId, message.CorrelationId);
                throw;
            }
        }

        private async Task<List<HistoricalDustData>> GetHistoricalData(int textileId)
        {
            return await _dbContext.HistoricalDustData
                .AsNoTracking()
                .Where(d => d.TextileId == textileId)
                .OrderByDescending(d => d.ReadingTime)
                .Take(30)
                .ToListAsync();
        }

        private InitialConditions ExtractInitialConditions(
            ISensorDataReceived message,
            List<HistoricalDustData> historicalData,
            Textile textile)
        {
            var temperature = message.Temperature
                ?? historicalData.Where(d => d.Temperature.HasValue).Select(d => d.Temperature.Value).DefaultIfEmpty(22m).Average();

            var humidity = message.Humidity
                ?? historicalData.Where(d => d.Humidity.HasValue).Select(d => d.Humidity.Value).DefaultIfEmpty(55m).Average();

            var holeDensity = message.HoleDensity.HasValue
                ? (double)message.HoleDensity.Value
                : historicalData.Any()
                    ? (double)historicalData.Average(d => d.HoleDensity)
                    : _populationConfig.DefaultInitialPestDensity;

            var fungiCFU = message.FungiCFU.HasValue
                ? (double)message.FungiCFU.Value
                : 100.0;

            if (message.FrassDensity.HasValue && message.FrassDensity.Value > 0)
            {
                holeDensity = Math.Max(holeDensity, (double)message.FrassDensity.Value * 0.5);
            }

            if (message.HoleCount.HasValue && message.HoleCount.Value > 0 && textile.AreaCm2 > 0)
            {
                var densityFromCount = (double)message.HoleCount.Value / (double)textile.AreaCm2 * 100;
                holeDensity = Math.Max(holeDensity, densityFromCount);
            }

            return new InitialConditions
            {
                Temperature = Math.Round(temperature, 2),
                Humidity = Math.Round(humidity, 2),
                PestDensity = Math.Max(0.01, Math.Round(holeDensity, 4)),
                FungiCFU = Math.Max(1.0, Math.Round(fungiCFU, 2))
            };
        }

        private class InitialConditions
        {
            public decimal Temperature { get; set; }
            public decimal Humidity { get; set; }
            public double PestDensity { get; set; }
            public double FungiCFU { get; set; }
        }
    }
}
