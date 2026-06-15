
using System;
using System.Collections.Generic;

namespace TextileMonitoring.API.Pest
{
    public class LotkaVolterraParameters
    {
        public decimal R { get; set; } = 0.05m;

        public decimal K { get; set; } = 12.0m;

        public decimal N0 { get; set; } = 0.5m;

        public decimal Alpha { get; set; } = 0.35m;

        public decimal Beta { get; set; } = 0.02m;

        public decimal Delta { get; set; } = 0.08m;

        public decimal P0 { get; set; } = 0.15m;

        public decimal EnvironmentFactor { get; set; } = 1.0m;

        public decimal CarryingCapacityForPredator { get; set; } = 2.5m;
    }

    public class LotkaVolterraResult
    {
        public int Day { get; set; }
        public decimal PestDensity { get; set; }
        public decimal PredatorDensity { get; set; }
        public decimal PredationRate { get; set; }
        public decimal NetGrowthRate { get; set; }
    }

    public class LotkaVolterraModel
    {
        public static List<LotkaVolterraResult> Simulate(LotkaVolterraParameters parameters, int horizonDays)
        {
            var results = new List<LotkaVolterraResult>(horizonDays + 1);

            decimal N = parameters.N0;
            decimal P = parameters.P0;
            decimal r = parameters.R * parameters.EnvironmentFactor;
            decimal K = parameters.K;
            decimal alpha = parameters.Alpha;
            decimal beta = parameters.Beta;
            decimal delta = parameters.Delta;
            decimal Kp = parameters.CarryingCapacityForPredator;

            for (int t = 0; t <= horizonDays; t++)
            {
                decimal logisticGrowth = r * N * (1.0m - N / K);
                decimal predationLoss = alpha * N * P;
                decimal dNdt = logisticGrowth - predationLoss;

                decimal predatorGrowth = beta * N * P;
                decimal predatorDeath = delta * P;
                decimal predatorLogistic = P * (1.0m - P / Kp);
                decimal dPdt = predatorGrowth - predatorDeath;
                dPdt *= (N > 0.1m ? 1.0m : 0.3m);
                dPdt = Math.Max(dPdt, -P * 0.1m);

                N = Math.Max(0.01m, N + dNdt);
                P = Math.Max(0.01m, P + dPdt);
                N = Math.Min(N, K * 1.05m);
                P = Math.Min(P, Kp * 1.1m);

                results.Add(new LotkaVolterraResult
                {
                    Day = t,
                    PestDensity = Math.Round(N, 4),
                    PredatorDensity = Math.Round(P, 4),
                    PredationRate = Math.Round(predationLoss, 4),
                    NetGrowthRate = Math.Round(dNdt, 4)
                });
            }

            return results;
        }

        public static LotkaVolterraParameters FitParameters<T>(List<T> historicalData, decimal temperature, decimal humidity)
            where T : class
        {
            var parameters = new LotkaVolterraParameters();

            if (historicalData == null || historicalData.Count < 2)
                return CalibrateByEnvironment(parameters, temperature, humidity);

            var values = new List<decimal>();
            foreach (var item in historicalData)
            {
                var prop = item.GetType().GetProperty("HoleDensity");
                if (prop != null && prop.GetValue(item) != null)
                    values.Add((decimal)prop.GetValue(item)!);
            }

            if (values.Count < 2)
                return CalibrateByEnvironment(parameters, temperature, humidity);

            values.Reverse();

            parameters.N0 = values.First();
            parameters.K = Math.Max(values.Max() * 1.4m, 5.0m);

            if (values.Count >= 3)
            {
                try
                {
                    int midIdx = values.Count / 2;
                    decimal N1 = values.First();
                    decimal N2 = values[midIdx];
                    decimal N3 = values.Last();

                    if (N1 > 0 && N3 < parameters.K * 0.95m)
                    {
                        double t1 = 0;
                        double t2 = midIdx;
                        double t3 = values.Count - 1;
                        double Kd = (double)parameters.K;

                        double r1 = N2 > N1 ? Math.Log((double)((Kd - (double)N1) * (double)N2) / ((double)N1 * (Kd - (double)N2))) / (t2 - t1) : 0.05;
                        double r2 = N3 > N2 ? Math.Log((double)((Kd - (double)N2) * (double)N3) / ((double)N2 * (Kd - (double)N3))) / (t3 - t2) : 0.05;

                        parameters.R = (decimal)((Math.Abs(r1) + Math.Abs(r2)) / 2.0);
                        parameters.R = Math.Clamp(parameters.R, 0.005m, 0.15m);
                    }
                }
                catch
                {
                }
            }

            parameters.P0 = EstimateInitialPredatorDensity(parameters.R, parameters.K, values);

            return CalibrateByEnvironment(parameters, temperature, humidity);
        }

        private static decimal EstimateInitialPredatorDensity(decimal r, decimal K, List<decimal> observations)
        {
            if (observations.Count < 10)
                return Math.Max(0.05m, r * K * 0.08m);

            decimal observedGrowth = 0;
            int count = 0;
            for (int i = 1; i < observations.Count; i++)
            {
                if (observations[i - 1] > 0)
                {
                    decimal delta = observations[i] - observations[i - 1];
                    decimal expectedLogistic = r * observations[i - 1] * (1 - observations[i - 1] / K);
                    decimal diff = expectedLogistic - delta;
                    if (diff > 0 && observations[i - 1] > 0.1m)
                    {
                        observedGrowth += diff / observations[i - 1];
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                decimal avgPredationEffect = observedGrowth / count;
                return Math.Clamp(avgPredationEffect * 0.6m, 0.02m, 1.0m);
            }

            return Math.Max(0.05m, r * K * 0.08m);
        }

        private static LotkaVolterraParameters CalibrateByEnvironment(LotkaVolterraParameters parameters, decimal temperature, decimal humidity)
        {
            decimal tempFactor = 1.0m;
            if (temperature > 28)
                tempFactor = 1.0m + (temperature - 28) * 0.06m;
            else if (temperature < 18)
                tempFactor = Math.Max(0.5m, 1.0m - (18 - temperature) * 0.04m);

            decimal humFactor = 1.0m;
            if (humidity > 65)
                humFactor = 1.0m + (humidity - 65) * 0.025m;
            else if (humidity < 45)
                humFactor = Math.Max(0.6m, 1.0m - (45 - humidity) * 0.015m);

            parameters.EnvironmentFactor = Math.Clamp(tempFactor * humFactor, 0.4m, 2.2m);

            parameters.Alpha = Math.Clamp(0.35m * tempFactor, 0.15m, 0.7m);
            parameters.Beta = Math.Clamp(0.02m * (0.8m + 0.4m * (humidity / 60m)), 0.008m, 0.05m);
            parameters.Delta = Math.Clamp(0.08m * (1.0m + Math.Max(0, 28m - temperature) * 0.02m), 0.04m, 0.15m);

            parameters.CarryingCapacityForPredator = Math.Clamp(
                parameters.K * 0.18m * (humidity / 55m),
                0.5m,
                4.0m
            );

            return parameters;
        }
    }
}
