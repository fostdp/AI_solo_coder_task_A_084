
using TextileMonitoring.Contracts.Messages;
using MildewService = TextileMonitoring.MildewGompertz;

namespace TextileMonitoring.Tests.Algorithms;

public class GompertzTests
{
    private readonly MildewService.GompertzPredictionService _service;

    public GompertzTests()
    {
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<MildewService.GompertzPredictionService>>();
        _service = new MildewService.GompertzPredictionService(loggerMock.Object);
    }

    [Fact]
    public void GompertzModel_BasicFormula_IsCorrect()
    {
        double A = 500.0;
        double B = 2.0;
        double C = 0.05;
        double t = 10.0;

        double y = A * Math.Exp(-B * Math.Exp(-C * t));

        Assert.True(y > 0 && y < A, "Gompertz 模型输出应该在 (0, A) 范围内");
    }

    [Theory]
    [InlineData(0, 67.667)]
    [InlineData(10, 210.718)]
    [InlineData(30, 423.400)]
    [InlineData(60, 495.419)]
    [InlineData(100, 499.955)]
    public void GompertzModel_GrowthCurve_IsSigmoid(double t, double expectedY)
    {
        double A = 500.0;
        double B = 2.0;
        double C = 0.05;

        double y = A * Math.Exp(-B * Math.Exp(-C * t));

        Assert.Equal(expectedY, y, 3);
    }

    [Fact]
    public void GompertzModel_AtTimeZero_IsCorrect()
    {
        double A = 500.0;
        double B = 2.0;
        double C = 0.05;

        double y0 = A * Math.Exp(-B);
        double y = A * Math.Exp(-B * Math.Exp(-C * 0));

        Assert.Equal(y0, y, 10);
    }

    [Fact]
    public void GompertzModel_Asymptote_IsCarryingCapacity()
    {
        double A = 500.0;
        double B = 2.0;
        double C = 0.05;

        double y_inf = A * Math.Exp(-B * Math.Exp(-C * 1000));

        Assert.True(Math.Abs(y_inf - A) < 0.001,
            "当 t→∞ 时，Gompertz 模型应该趋近于承载量 A");
    }

    [Fact]
    public void GompertzModel_InflectionPoint_IsCorrect()
    {
        double A = 500.0;
        double B = 2.0;
        double C = 0.05;

        double t_inflection = Math.Log(B) / C;
        double y_inflection = A / Math.E;

        double y = A * Math.Exp(-B * Math.Exp(-C * t_inflection));

        Assert.Equal(y_inflection, y, 10);
    }

    [Fact]
    public void GompertzModel_DoublingTime_IsCorrect()
    {
        double C = 0.05;

        double doublingTimeHours = Math.Log(2) / C * 24;

        Assert.Equal(332.71, doublingTimeHours, 2);
    }

    [Theory]
    [InlineData(100, 500, 0.015, 30, 250.0)]
    [InlineData(200, 500, 0.02, 30, 380.0)]
    [InlineData(50, 400, 0.01, 60, 200.0)]
    public void GompertzModel_Parameters_AffectGrowth(
        double y0, double A, double C, int horizon, double expectedMin)
    {
        double B = Math.Log(A / y0);

        double finalY = A * Math.Exp(-B * Math.Exp(-C * horizon));

        Assert.True(finalY >= expectedMin,
            $"最终预测值 {finalY} 应该至少为 {expectedMin}");
        Assert.True(finalY <= A,
            $"最终预测值 {finalY} 不应该超过承载量 {A}");
    }

    [Fact]
    public void GompertzModel_GrowthRate_DecreasesWithTime()
    {
        double A = 500.0;
        double B = 2.0;
        double C = 0.05;

        double rateEarly = (A * Math.Exp(-B * Math.Exp(-C * 5)) - A * Math.Exp(-B * Math.Exp(-C * 0))) / 5;
        double rateLate = (A * Math.Exp(-B * Math.Exp(-C * 50)) - A * Math.Exp(-B * Math.Exp(-C * 45))) / 5;

        Assert.True(rateEarly > rateLate,
            "Gompertz 模型的生长速率应该随时间递减");
    }

    [Fact]
    public async Task GompertzService_GeneratesValidPrediction()
    {
        var request = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 22.5,
            Humidity = 65.0,
            FungiCFU = 150.0,
            SporeCount = 200.0,
            DominantFungiType = "Aspergillus"
        };

        var result = await _service.PredictAsync(request, 30);

        Assert.NotNull(result);
        Assert.Equal(1, result.TextileId);
        Assert.Equal(30, result.HorizonDays);
        Assert.NotNull(result.DataPoints);
        Assert.Equal(31, result.DataPoints.Count);
        Assert.True(result.FinalPredictedCFU > 0);
        Assert.True(result.InflectionPointDay > 0);
        Assert.True(result.DoublingTimeHours > 0);
        Assert.InRange(result.Confidence, 0.0, 1.0);
    }

    [Theory]
    [InlineData(100, 150)]
    [InlineData(200, 300)]
    [InlineData(300, 400)]
    public async Task GompertzService_HigherInitialCFU_ResultsInHigherPrediction(
        double initialCFU, double expectedMin)
    {
        var request = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 22.5,
            Humidity = 65.0,
            FungiCFU = initialCFU,
            SporeCount = initialCFU * 1.5,
            DominantFungiType = "Aspergillus"
        };

        var result = await _service.PredictAsync(request, 30);

        Assert.True(result.FinalPredictedCFU >= expectedMin,
            $"初始CFU {initialCFU} 时，最终预测值应至少为 {expectedMin}，实际为 {result.FinalPredictedCFU}");
    }

    [Fact]
    public async Task GompertzService_HumidityAffectsGrowthRate()
    {
        var requestLow = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 22.5,
            Humidity = 30.0,
            FungiCFU = 150.0,
            SporeCount = 200.0,
            DominantFungiType = "Aspergillus"
        };

        var requestHigh = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 22.5,
            Humidity = 80.0,
            FungiCFU = 150.0,
            SporeCount = 200.0,
            DominantFungiType = "Aspergillus"
        };

        var resultLow = await _service.PredictAsync(requestLow, 30);
        var resultHigh = await _service.PredictAsync(requestHigh, 30);

        Assert.True(resultHigh.FinalPredictedCFU > resultLow.FinalPredictedCFU,
            "湿度较高时，预测的霉菌浓度应该更高");
    }

    [Fact]
    public async Task GompertzService_TriggersAlertAtThreshold()
    {
        var request = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = 25.0,
            Humidity = 75.0,
            FungiCFU = 250.0,
            SporeCount = 350.0,
            DominantFungiType = "Aspergillus"
        };

        var result = await _service.PredictAsync(request, 30);

        if (result.FinalPredictedCFU > 200)
        {
            Assert.Equal(2, result.RiskLevel);
        }
    }

    [Fact]
    public void GompertzModel_ParameterExtraction_IsCorrect()
    {
        double y0 = 100.0;
        double A = 500.0;
        double t1 = 10.0;
        double y1 = 250.0;

        double B = Math.Log(A / y0);
        double C = -Math.Log(-Math.Log(y1 / A) / B) / t1;

        double y1_calculated = A * Math.Exp(-B * Math.Exp(-C * t1));

        Assert.Equal(y1, y1_calculated, 3);
    }
}
