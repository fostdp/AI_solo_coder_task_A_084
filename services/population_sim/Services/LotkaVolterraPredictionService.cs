using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.OdeSolvers;
using Microsoft.Extensions.Options;
using PopulationSim.Service.Models;
using TextileMonitoring.Contracts.Messages;

namespace PopulationSim.Service.Services;

public class LotkaVolterraPredictionService
{
    private readonly PopulationModelConfig _modelConfig;
    private readonly ILogger<LotkaVolterraPredictionService> _logger;

    public LotkaVolterraPredictionService(
        IOptions<PopulationModelConfig> modelConfig,
        ILogger<LotkaVolterraPredictionService> logger)
    {
        _modelConfig = modelConfig.Value;
        _logger = logger;
    }

    public PopulationPredictionGenerated SolvePrediction(
        int textileId,
        double avgTemperature,
        double avgHumidity,
        double initialPestDensity,
        double initialPredatorDensity,
        int horizonDays,
        Guid correlationId)
    {
        _logger.Debug(
            "Starting Lotka-Volterra prediction for TextileId: {TextileId}, T={Temperature}, H={Humidity}, N0={N0}, P0={P0}",
            textileId, avgTemperature, avgHumidity, initialPestDensity, initialPredatorDensity);

        var envFactor = CalculateEnvironmentFactor(avgTemperature, avgHumidity);
        var results = SolveWithAdaptiveStep(
            initialPestDensity,
            initialPredatorDensity,
            envFactor,
            horizonDays);

        return BuildPredictionResult(
            textileId,
            avgTemperature,
            avgHumidity,
            initialPestDensity,
            initialPredatorDensity,
            horizonDays,
            results,
            correlationId);
    }

    private List<OdeState> SolveWithAdaptiveStep(
        double initialPest,
        double initialPredator,
        double envFactor,
        int totalDays)
    {
        var r = _modelConfig.DefaultGrowthRate;
        var K = _modelConfig.DefaultCarryingCapacity;
        var alpha = _modelConfig.AlphaPredation;
        var beta = _modelConfig.BetaConversion;
        var delta = _modelConfig.DeltaMortality;
        var Kp = _modelConfig.PredatorCarryingCapacity;

        var initialStep = _modelConfig.OdeInitialStep;
        var minStep = _modelConfig.OdeMinStep;
        var maxStep = _modelConfig.OdeMaxStep;
        var divergenceThreshold = _modelConfig.DivergenceThreshold;
        var autoTuneCoeff = _modelConfig.AutoTuneCoefficient;

        Func<double, Vector<double>, Vector<double>> lotkaVolterraSystem = (t, y) =>
        {
            double N = Math.Max(1e-6, y[0]);
            double P = Math.Max(1e-6, y[1]);

            double rAdj = r * envFactor;
            double dNdt = rAdj * N * (1.0 - N / K)
                        - alpha * N * P;

            double betaAdj = beta * envFactor;
            double dPdt = betaAdj * N * P
                        - delta * P
                        - delta * 0.1 * P * (P / Kp);

            return Vector<double>.Build.DenseOfArray(new[] { dNdt, dPdt });
        };

        var y0 = Vector<double>.Build.DenseOfArray(new[]
        {
            Math.Max(0.01, initialPest),
            Math.Max(0.01, initialPredator)
        });

        var results = new List<OdeState>(totalDays + 1)
        {
            new OdeState
            {
                Day = 0,
                PestDensity = y0[0],
                PredatorDensity = y0[1],
                PredationRate = CalculatePredationRate(y0[0], y0[1], alpha),
                NetGrowthRate = CalculateNetGrowthRate(y0[0], y0[1], rAdj: r * envFactor, K, alpha)
            }
        };

        double currentTime = 0.0;
        double currentStep = initialStep;
        var currentState = y0;

        while (currentTime < totalDays)
        {
            if (currentTime + currentStep > totalDays)
                currentStep = totalDays - currentTime;

            var k1 = lotkaVolterraSystem(currentTime, currentState);
            var k2 = lotkaVolterraSystem(currentTime + currentStep * 0.5, currentState + currentStep * 0.5 * k1);
            var k3 = lotkaVolterraSystem(currentTime + currentStep * 0.5, currentState + currentStep * 0.5 * k2);
            var k4 = lotkaVolterraSystem(currentTime + currentStep, currentState + currentStep * k3);

            var nextState = currentState + (currentStep / 6.0) * (k1 + 2.0 * k2 + 2.0 * k3 + k4);

            var k2a = lotkaVolterraSystem(currentTime + currentStep * 0.25, currentState + currentStep * 0.25 * k1);
            var k3a = lotkaVolterraSystem(currentTime + currentStep * 0.5, currentState + currentStep * 0.25 * k1 + currentStep * 0.25 * k2a);
            var midState = currentState + (currentStep / 12.0) * (k1 + 3.0 * k2a + 3.0 * k3a + k4);

            double error = nextState.Subtract(midState).L2Norm();

            if (error > divergenceThreshold || ContainsInvalidValues(nextState))
            {
                currentStep = Math.Max(minStep, currentStep * 0.5);
                _logger.Debug(
                    "Step rejected at t={Time}, error={Error}, reducing step to {Step}",
                    currentTime, error, currentStep);
                continue;
            }

            double growthRatio = currentState[0] > 1e-6
                ? Math.Abs(nextState[0] - currentState[0]) / Math.Abs(currentState[0])
                : 0;

            double adaptiveFactor = 1.0;
            if (growthRatio > 0.1)
                adaptiveFactor = 0.8;
            else if (growthRatio < 0.01)
                adaptiveFactor = 1.2;

            currentTime += currentStep;
            currentState = nextState;

            currentState[0] = Math.Max(1e-4, Math.Min(currentState[0], K * 1.1));
            currentState[1] = Math.Max(1e-4, Math.Min(currentState[1], Kp * 1.1));

            int currentDay = (int)Math.Round(currentTime);
            int lastDay = results[^1].Day;

            if (currentDay > lastDay)
            {
                for (int d = lastDay + 1; d <= currentDay && d <= totalDays; d++)
                {
                    double interpolatedTime = d;
                    var (interpPest, interpPredator) = InterpolateState(
                        currentTime - currentStep, currentState - (nextState - currentState),
                        currentTime, currentState,
                        interpolatedTime);

                    results.Add(new OdeState
                    {
                        Day = d,
                        PestDensity = Math.Round(interpPest, 6),
                        PredatorDensity = Math.Round(interpPredator, 6),
                        PredationRate = Math.Round(CalculatePredationRate(interpPest, interpPredator, alpha), 6),
                        NetGrowthRate = Math.Round(CalculateNetGrowthRate(interpPest, interpPredator, r * envFactor, K, alpha), 6)
                    });
                }
            }

            double targetStep = currentStep * adaptiveFactor * (1.0 - autoTuneCoeff * error);
            currentStep = Math.Clamp(targetStep, minStep, maxStep);
        }

        _logger.Debug(
            "ODE solver completed for {Days} days, {Points} points, final N={N}, P={P}",
            totalDays, results.Count, results[^1].PestDensity, results[^1].PredatorDensity);

        return results;
    }

    private static (double Pest, double Predator) InterpolateState(
        double t1, Vector<double> y1,
        double t2, Vector<double> y2,
        double targetT)
    {
        double factor = (targetT - t1) / (t2 - t1);
        return (
            y1[0] + factor * (y2[0] - y1[0]),
            y1[1] + factor * (y2[1] - y1[1])
        );
    }

    private static bool ContainsInvalidValues(Vector<double> v)
    {
        for (int i = 0; i < v.Count; i++)
        {
            if (double.IsNaN(v[i]) || double.IsInfinity(v[i]) || v[i] < -1e-10)
                return true;
        }
        return false;
    }

    private double CalculateEnvironmentFactor(double temperature, double humidity)
    {
        var tempOpt = _modelConfig.TemperatureOptimal;
        var humOpt = _modelConfig.HumidityOptimal;

        double tempDiff = Math.Abs(temperature - tempOpt);
        double humDiff = Math.Abs(humidity - humOpt);

        double tempFactor = tempDiff switch
        {
            <= 3 => 1.0,
            <= 7 => 0.95,
            <= 12 => 0.85,
            <= 18 => 0.7,
            _ => 0.55
        };

        double humFactor = humDiff switch
        {
            <= 8 => 1.0,
            <= 15 => 0.9,
            <= 25 => 0.78,
            <= 35 => 0.65,
            _ => 0.5
        };

        return tempFactor * humFactor;
    }

    private static double CalculatePredationRate(double pest, double predator, double alpha)
    {
        if (pest < 1e-4) return 0;
        return Math.Min(1.0, alpha * predator / pest);
    }

    private static double CalculateNetGrowthRate(double pest, double predator, double r, double K, double alpha)
    {
        if (pest < 1e-4) return 0;
        return r * (1.0 - pest / K) - alpha * predator;
    }

    private PopulationPredictionGenerated BuildPredictionResult(
        int textileId,
        double avgTemperature,
        double avgHumidity,
        double initialPest,
        double initialPredator,
        int horizonDays,
        List<OdeState> results,
        Guid correlationId)
    {
        var predictionPoints = results.Select(r => new PredictionPoint
        {
            Day = r.Day,
            PestDensity = r.PestDensity,
            PredatorDensity = r.PredatorDensity,
            PredationRate = r.PredationRate,
            NetGrowthRate = r.NetGrowthRate
        }).ToList();

        var maxPest = results.Max(r => r.PestDensity);
        var maxPredator = results.Max(r => r.PredatorDensity);
        var finalPest = results[^1].PestDensity;
        var finalPredator = results[^1].PredatorDensity;

        var riskLevel = CalculateRiskLevel(maxPest, finalPredator);
        var confidence = CalculateConfidence(results.Count, horizonDays);

        return new PopulationPredictionGenerated
        {
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            TextileId = textileId,
            HorizonDays = horizonDays,
            ModelType = "LotkaVolterra",
            InitialPestDensity = Math.Round(initialPest, 6),
            InitialPredatorDensity = Math.Round(initialPredator, 6),
            AvgTemperature = Math.Round(avgTemperature, 2),
            AvgHumidity = Math.Round(avgHumidity, 2),
            PredictionPoints = predictionPoints,
            MaxPestDensity = Math.Round(maxPest, 6),
            MaxPredatorDensity = Math.Round(maxPredator, 6),
            FinalPestDensity = Math.Round(finalPest, 6),
            FinalPredatorDensity = Math.Round(finalPredator, 6),
            RiskLevel = riskLevel,
            Confidence = Math.Round(confidence, 4),
            AlphaPredation = _modelConfig.AlphaPredation,
            BetaConversion = _modelConfig.BetaConversion,
            DeltaMortality = _modelConfig.DeltaMortality
        };
    }

    private string CalculateRiskLevel(double maxPest, double finalPredator)
    {
        if (maxPest >= _modelConfig.PestDensityCritical)
            return "Critical";
        if (maxPest >= _modelConfig.PestDensityWarning)
            return "High";
        if (finalPredator >= _modelConfig.PredatorDensityWarning)
            return "Medium";
        if (maxPest >= 1.0)
            return "Low";
        return "Normal";
    }

    private static double CalculateConfidence(int dataPoints, int expectedDays)
    {
        double coverageRatio = (double)dataPoints / (expectedDays + 1);
        return Math.Min(0.98, 0.6 + 0.35 * coverageRatio);
    }
}

public class OdeState
{
    public int Day { get; set; }
    public double PestDensity { get; set; }
    public double PredatorDensity { get; set; }
    public double PredationRate { get; set; }
    public double NetGrowthRate { get; set; }
}
