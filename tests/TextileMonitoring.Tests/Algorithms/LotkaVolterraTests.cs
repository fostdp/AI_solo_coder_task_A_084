
using PopulationSim.Service.Models;
using PopulationSim.Service.Services;
using TextileMonitoring.Contracts.Messages;

namespace TextileMonitoring.Tests.Algorithms;

public class LotkaVolterraTests
{
    private readonly LotkaVolterraPredictionService _service;
    private readonly PopulationModelConfig _config;

    public LotkaVolterraTests()
    {
        _config = new PopulationModelConfig
        {
            GrowthRateR = 0.05,
            CarryingCapacityK = 12.0,
            PredationAlpha = 0.35,
            ConversionBeta = 0.02,
            MortalityDelta = 0.08,
            PredatorCarryingCapacityKp = 2.5,
            InitialPredatorDensity = 0.15,
            TemperatureSensitivity = 0.06,
            HumiditySensitivity = 0.025,
            SynergyInteractionPhi = 1.35,
            OdeMinStep = 0.005,
            OdeMaxStep = 0.5,
            OdeDivergenceThreshold = 0.5
        };

        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<LotkaVolterraPredictionService>>();
        var windowManager = new PredictionWindowManager(new Microsoft.Extensions.Options.IOptionsSnapshot<PredictionWindowConfig>());
        _service = new LotkaVolterraPredictionService(
            loggerMock.Object,
            new Microsoft.Extensions.Options.IOptionsSnapshot<PopulationModelConfig>(),
            windowManager);
    }

    [Fact]
    public void LotkaVolterraParameters_DefaultValues_AreCorrect()
    {
        Assert.Equal(0.05, _config.GrowthRateR, 4);
        Assert.Equal(12.0, _config.CarryingCapacityK, 4);
        Assert.Equal(0.35, _config.PredationAlpha, 4);
        Assert.Equal(0.02, _config.ConversionBeta, 4);
        Assert.Equal(0.08, _config.MortalityDelta, 4);
    }

    [Fact]
    public void LotkaVolterraModel_WithPredatorTerm_ReducesPopulation()
    {
        var parameters = new
        {
            R = 0.05,
            K = 12.0,
            Alpha = 0.35,
            Beta = 0.02,
            Delta = 0.08,
            N0 = 2.0,
            P0 = 0.5
        };

        var dN_dt_withoutPredator = parameters.R * parameters.N0 * (1 - parameters.N0 / parameters.K);
        var dN_dt_withPredator = parameters.R * parameters.N0 * (1 - parameters.N0 / parameters.K)
                                - parameters.Alpha * parameters.N0 * parameters.P0;

        Assert.True(dN_dt_withPredator < dN_dt_withoutPredator,
            "引入天敌捕食项后，种群增长率应该降低");
    }

    [Theory]
    [InlineData(0.1, 0.1, 0.00495, 0.0)]
    [InlineData(1.0, 0.5, 0.28333, 0.06)]
    [InlineData(5.0, 1.0, -0.625, 0.02)]
    public void LotkaVolterraModel_Derivatives_AreCalculatedCorrectly(
        double N, double P, double expectedDnDt, double expectedDpDt)
    {
        var r = 0.05;
        var K = 12.0;
        var alpha = 0.35;
        var beta = 0.02;
        var delta = 0.08;

        var dN_dt = r * N * (1 - N / K) - alpha * N * P;
        var dP_dt = beta * N * P - delta * P;

        Assert.Equal(expectedDnDt, dN_dt, 3);
        Assert.Equal(expectedDpDt, dP_dt, 3);
    }

    [Fact]
    public void LotkaVolterraModel_PopulationCannotExceedCarryingCapacity()
    {
        var r = 0.05;
        var K = 12.0;
        var alpha = 0.35;
        var P = 0.5;

        var dN_dt_at_K = r * K * (1 - K / K) - alpha * K * P;

        Assert.True(dN_dt_at_K < 0,
            "当种群密度达到环境承载量K时，增长率应该为负（受捕食影响）");
    }

    [Fact]
    public void LotkaVolterraModel_PredatorPopulation_IncreasesWithPrey()
    {
        var beta = 0.02;
        var delta = 0.08;
        var P = 0.5;

        var dP_dt_lowPrey = beta * 1.0 * P - delta * P;
        var dP_dt_highPrey = beta * 10.0 * P - delta * P;

        Assert.True(dP_dt_highPrey > dP_dt_lowPrey,
            "猎物密度增加时，捕食者种群增长率应该增加");
    }

    [Fact]
    public void LotkaVolterraModel_EquilibriumPoint_IsStable()
    {
        var r = 0.05;
        var K = 12.0;
        var alpha = 0.35;
        var beta = 0.02;
        var delta = 0.08;

        var N_star = delta / beta;
        var P_star = r * (1 - N_star / K) / alpha;

        var dN_dt = r * N_star * (1 - N_star / K) - alpha * N_star * P_star;
        var dP_dt = beta * N_star * P_star - delta * P_star;

        Assert.Equal(0, dN_dt, 10);
        Assert.Equal(0, dP_dt, 10);
    }

    [Fact]
    public async Task PredictionService_GeneratesValidPrediction()
    {
        var request = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 22.5,
            Humidity = 55.0,
            FrassDensity = 1.5,
            HoleCount = 3
        };

        var result = await _service.PredictAsync(request, 30);

        Assert.NotNull(result);
        Assert.Equal(1, result.TextileId);
        Assert.Equal(30, result.HorizonDays);
        Assert.NotNull(result.DataPoints);
        Assert.Equal(31, result.DataPoints.Count);
        Assert.True(result.FinalPredictedDensity >= 0);
        Assert.InRange(result.Confidence, 0.0, 1.0);
    }

    [Theory]
    [InlineData(0.5, 2.0)]
    [InlineData(1.5, 3.5)]
    [InlineData(3.0, 5.0)]
    public async Task PredictionService_HigherInitialDensity_ResultsInHigherFinalPrediction(
        double initialDensity, double expectedMin)
    {
        var request = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 22.5,
            Humidity = 55.0,
            FrassDensity = initialDensity,
            HoleCount = (int)(initialDensity * 2)
        };

        var result = await _service.PredictAsync(request, 30);

        Assert.True(result.FinalPredictedDensity >= expectedMin,
            $"初始密度 {initialDensity} 时，最终预测值应至少为 {expectedMin}，实际为 {result.FinalPredictedDensity}");
    }

    [Fact]
    public async Task PredictionService_TemperatureAffectsGrowthRate()
    {
        var requestLow = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 15.0,
            Humidity = 55.0,
            FrassDensity = 1.5,
            HoleCount = 3
        };

        var requestHigh = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 30.0,
            Humidity = 55.0,
            FrassDensity = 1.5,
            HoleCount = 3
        };

        var resultLow = await _service.PredictAsync(requestLow, 30);
        var resultHigh = await _service.PredictAsync(requestHigh, 30);

        Assert.True(resultHigh.FinalPredictedDensity > resultLow.FinalPredictedDensity,
            "温度较高时，预测的种群密度应该更高");
    }

    [Fact]
    public async Task PredictionService_HumidityAffectsGrowthRate()
    {
        var requestLow = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 22.5,
            Humidity = 30.0,
            FrassDensity = 1.5,
            HoleCount = 3
        };

        var requestHigh = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 22.5,
            Humidity = 80.0,
            FrassDensity = 1.5,
            HoleCount = 3
        };

        var resultLow = await _service.PredictAsync(requestLow, 30);
        var resultHigh = await _service.PredictAsync(requestHigh, 30);

        Assert.True(resultHigh.FinalPredictedDensity > resultLow.FinalPredictedDensity,
            "湿度较高时，预测的种群密度应该更高");
    }
}
