using MathNet.Numerics.LinearAlgebra;
using Microsoft.Extensions.Options;
using TextileMonitoring.Infrastructure.Configuration;
using TextileMonitoring.Messages.Events;

namespace TextileMonitoring.PopulationSim
{
    public class LotkaVolterraPredictionService
    {
        private readonly PopulationModelConfig _populationConfig;
        private readonly MildewModelConfig _mildewConfig;
        private readonly AlertThresholdsConfig _thresholdsConfig;

        public LotkaVolterraPredictionService(
            IOptions<PopulationModelConfig> populationConfig,
            IOptions<MildewModelConfig> mildewConfig,
            IOptions<AlertThresholdsConfig> thresholdsConfig)
        {
            _populationConfig = populationConfig.Value;
            _mildewConfig = mildewConfig.Value;
            _thresholdsConfig = thresholdsConfig.Value;
        }

        public PredictionCalculated SolvePrediction(
            int textileId,
            string textileName,
            decimal temperature,
            decimal humidity,
            double initialPestDensity,
            double initialPredatorDensity,
            double initialFungiCFU,
            int? horizonDays = null)
        {
            var horizon = horizonDays ?? _populationConfig.DefaultHorizonDays;
            var tempFactor = CalculateTemperatureFactor(temperature);
            var humFactor = CalculateHumidityFactor(humidity);
            var envFactor = tempFactor * humFactor;

            var results = SolveCoupledEquations(
                initialPestDensity,
                initialPredatorDensity,
                initialFungiCFU,
                envFactor,
                horizon);

            return BuildPredictionCalculatedEvent(
                textileId,
                textileName,
                temperature,
                humidity,
                horizon,
                results);
        }

        private List<OdeSolverResult> SolveCoupledEquations(
            double initialPestDensity,
            double initialPredatorDensity,
            double initialFungiCFU,
            double envFactor,
            int totalDays)
        {
            var r = _populationConfig.DefaultGrowthRate;
            var K = _populationConfig.DefaultCarryingCapacity;
            var alpha = _populationConfig.PredationEfficiencyAlpha;
            var beta = _populationConfig.PredatorConversionBeta;
            var delta = _populationConfig.PredatorMortalityDelta;
            var Kp = _populationConfig.PredatorCarryingCapacity;
            var gamma = _populationConfig.SynergyGamma;
            var rho = _mildewConfig.DefaultGrowthRateRho;
            var Kf = _mildewConfig.DefaultCarryingCapacityKf;

            var initialStep = _populationConfig.OdeInitialStep;
            var minStep = _populationConfig.OdeMinStep;
            var maxStep = _populationConfig.OdeMaxStep;
            var divergenceThreshold = _populationConfig.DivergenceThreshold;

            Func<double, Vector<double>, Vector<double>> coupledSystem = (t, y) =>
            {
                double N = Math.Max(0.0, y[0]);
                double P = Math.Max(0.0, y[1]);
                double F = Math.Max(0.0, y[2]);

                double rAdj = r * envFactor;
                double dNdt = rAdj * N * (1.0 - N / K)
                            - alpha * N * P
                            - gamma * N * F * 0.1;

                double pestRatio = N > 0.1 ? 1.0 : 0.3;
                double dPdt = (beta * N * P - delta * P) * pestRatio;
                dPdt = Math.Max(dPdt, -P * 0.1);

                double rhoAdj = rho * envFactor;
                double Fadj = Math.Max(1.0, F);
                double dFdt = F <= 0 ? 0.0 : rhoAdj * F * Math.Log(Math.Max(1.001, Kf / Fadj))
                              + beta * 0.001 * N * F * 0.05;

                return Vector<double>.Build.DenseOfArray(new[]
                {
                    dNdt, dPdt, dFdt
                });
            };

            var y0 = Vector<double>.Build.DenseOfArray(new[]
            {
                Math.Max(0.01, initialPestDensity),
                Math.Max(0.01, initialPredatorDensity),
                Math.Max(1.0, initialFungiCFU)
            });

            double h = initialStep;
            double currentTime = 0.0;
            var currentState = y0;
            var results = new List<OdeSolverResult>((int)totalDays + 2)
            {
                new OdeSolverResult
                {
                    Time = 0,
                    State = new PopulationState
                    {
                        PestDensity = y0[0],
                        PredatorDensity = y0[1],
                        FungiCFU = y0[2]
                    },
                    SynergyRisk = CalculateSynergyRisk(y0[0], y0[2])
                }
            };

            while (currentTime < totalDays)
            {
                if (currentTime + h > totalDays)
                    h = totalDays - currentTime;

                var k1 = coupledSystem(currentTime, currentState);
                double k1n = k1[0], k1p = k1[1], k1f = k1[2];

                var y2 = currentState + h * 0.5 * Vector<double>.Build.DenseOfArray(new[] { k1n, k1p, k1f });
                var s2 = coupledSystem(currentTime + h * 0.5, y2);
                double k2n = s2[0], k2p = s2[1], k2f = s2[2];

                var y3 = currentState + h * 0.5 * Vector<double>.Build.DenseOfArray(new[] { k2n, k2p, k2f });
                var s3 = coupledSystem(currentTime + h * 0.5, y3);
                double k3n = s3[0], k3p = s3[1], k3f = s3[2];

                var y4 = currentState + h * Vector<double>.Build.DenseOfArray(new[] { k3n, k3p, k3f });
                var s4 = coupledSystem(currentTime + h, y4);
                double k4n = s4[0], k4p = s4[1], k4f = s4[2];

                var next = currentState + (h / 6.0) * Vector<double>.Build.DenseOfArray(new[]
                {
                    k1n + 2*k2n + 2*k3n + k4n,
                    k1p + 2*k2p + 2*k3p + k4p,
                    k1f + 2*k2f + 2*k3f + k4f
                });

                double growthRatio = currentState[0] > 1e-6
                    ? Math.Abs(next[0] - currentState[0]) / Math.Abs(currentState[0])
                    : 0;

                if (growthRatio > divergenceThreshold || double.IsNaN(next[0]) || double.IsNaN(next[1]) || double.IsNaN(next[2]))
                {
                    h = Math.Max(minStep, h * 0.5);
                    continue;
                }

                currentTime += h;
                currentState = next;
                currentState[0] = Math.Max(0.001, Math.Min(currentState[0], K * 1.05));
                currentState[1] = Math.Max(0.001, Math.Min(currentState[1], Kp * 1.1));
                currentState[2] = Math.Max(0.1, Math.Min(currentState[2], Kf * 1.2));

                int expectedDay = (int)Math.Round(currentTime);
                if (expectedDay > results[^1].Time - 0.001 || Math.Abs(currentTime - expectedDay) < 0.001)
                {
                    results.Add(new OdeSolverResult
                    {
                        Time = Math.Round(currentTime, 3),
                        State = new PopulationState
                        {
                            PestDensity = Math.Round(currentState[0], 4),
                            PredatorDensity = Math.Round(currentState[1], 4),
                            FungiCFU = Math.Round(currentState[2], 2)
                        },
                        SynergyRisk = Math.Round(CalculateSynergyRisk(currentState[0], currentState[2]), 2)
                    });
                }

                if (growthRatio < 0.02 && h < maxStep)
                    h = Math.Min(maxStep, h * 1.2);
            }

            return results;
        }

        private double CalculateTemperatureFactor(decimal temperature)
        {
            var tempOpt = _populationConfig.TemperatureOptimal;
            var tempDiff = Math.Abs((double)temperature - tempOpt);

            if (tempDiff <= 5)
                return 1.0;
            if (tempDiff <= 10)
                return 0.9;
            if (tempDiff <= 15)
                return 0.75;
            return 0.6;
        }

        private double CalculateHumidityFactor(decimal humidity)
        {
            var humOpt = _populationConfig.HumidityOptimal;
            var humDiff = Math.Abs((double)humidity - humOpt);

            if (humDiff <= 10)
                return 1.0;
            if (humDiff <= 20)
                return 0.85;
            if (humDiff <= 30)
                return 0.7;
            return 0.55;
        }

        private double CalculateSynergyRisk(double pestDensity, double fungiCFU)
        {
            double hNorm = Math.Min(1.0, pestDensity / 10.0);
            double fNorm = Math.Min(1.0, fungiCFU / 500.0);
            double phi = _mildewConfig.SynergyInteractionPhi;
            double alpha = 0.5, beta = 0.35, gamma = 0.15;
            double R = Math.Sqrt(alpha * hNorm * hNorm + beta * fNorm * fNorm + gamma * hNorm * fNorm * phi);
            return Math.Min(100.0, R * 100.0);
        }

        private double CalculatePredationRate(double pestDensity, double predatorDensity)
        {
            if (pestDensity < 0.001) return 0;
            var alpha = _populationConfig.PredationEfficiencyAlpha;
            return Math.Min(1.0, alpha * predatorDensity / pestDensity);
        }

        private PredictionCalculated BuildPredictionCalculatedEvent(
            int textileId,
            string textileName,
            decimal temperature,
            decimal humidity,
            int horizonDays,
            List<OdeSolverResult> results)
        {
            var startDate = DateTime.UtcNow.Date;
            var dataPoints = new List<PredictionPoint>();
            decimal maxPredictedHoleDensity = 0;
            double finalPredatorDensity = 0;
            double finalPredationRate = 0;

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var holeDensity = (decimal)result.State.PestDensity;

                if (holeDensity > maxPredictedHoleDensity)
                    maxPredictedHoleDensity = holeDensity;

                if (i == results.Count - 1)
                {
                    finalPredatorDensity = result.State.PredatorDensity;
                    finalPredationRate = CalculatePredationRate(result.State.PestDensity, result.State.PredatorDensity);
                }

                dataPoints.Add(new PredictionPoint
                {
                    Date = startDate.AddDays(result.Time),
                    PredictedHoleDensity = Math.Round(holeDensity, 4),
                    PredatorDensity = Math.Round(result.State.PredatorDensity, 4),
                    PredationRate = Math.Round(CalculatePredationRate(result.State.PestDensity, result.State.PredatorDensity), 4)
                });
            }

            var riskLevel = CalculateRiskLevel(maxPredictedHoleDensity);
            var confidence = CalculateConfidence(results.Count);

            return new PredictionCalculated
            {
                CorrelationId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                TextileId = textileId,
                TextileName = textileName,
                HorizonDays = horizonDays,
                Model = _populationConfig.DefaultModel,
                MaxPredictedHoleDensity = Math.Round(maxPredictedHoleDensity, 4),
                PredatorDensity = Math.Round(finalPredatorDensity, 4),
                PredationRate = Math.Round(finalPredationRate, 4),
                RiskLevel = riskLevel,
                Confidence = Math.Round(confidence, 2),
                Temperature = temperature,
                Humidity = humidity,
                DataPoints = dataPoints
            };
        }

        private int CalculateRiskLevel(decimal holeDensity)
        {
            return holeDensity switch
            {
                < _thresholdsConfig.HoleDensityWarning => 0,
                < _thresholdsConfig.HoleDensityCritical => 1,
                < 8.0m => 2,
                _ => 3
            };
        }

        private double CalculateConfidence(int dataPoints)
        {
            if (dataPoints >= 30)
                return 0.92;
            if (dataPoints >= 20)
                return 0.85;
            if (dataPoints >= 10)
                return 0.75;
            return 0.65;
        }

        private class PopulationState
        {
            public double PestDensity { get; set; }
            public double PredatorDensity { get; set; }
            public double FungiCFU { get; set; }
        }

        private class OdeSolverResult
        {
            public double Time { get; set; }
            public PopulationState State { get; set; } = new();
            public double SynergyRisk { get; set; }
        }
    }
}
