
using TextileMonitoring.Contracts.Messages;
using TextileMonitoring.Contracts.RabbitMQ;

namespace TextileMonitoring.Tests.Contracts;

public class MessageContractsTests
{
    [Fact]
    public void SensorDataReceived_DefaultValues_AreCorrect()
    {
        var message = new SensorDataReceived();

        Assert.NotEqual(Guid.Empty, message.CorrelationId);
        Assert.True(message.Timestamp <= DateTime.UtcNow);
        Assert.Equal(string.Empty, message.SensorCode);
        Assert.Equal(string.Empty, message.SensorType);
        Assert.Equal(0, message.TextileId);
    }

    [Fact]
    public void SensorDataReceived_Properties_CanBeSet()
    {
        var correlationId = Guid.NewGuid();
        var timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var message = new SensorDataReceived
        {
            CorrelationId = correlationId,
            Timestamp = timestamp,
            SensorCode = "DUS-001",
            SensorType = "Dust",
            TextileId = 1,
            Temperature = 22.5,
            Humidity = 55.0,
            FrassDensity = 2.5,
            HoleCount = 3,
            PM2_5 = 35.0,
            PM10 = 60.0,
            SporeCount = 150.0,
            FungiCFU = 180.0,
            DominantFungiType = "Aspergillus",
            SignalStrength = -45
        };

        Assert.Equal(correlationId, message.CorrelationId);
        Assert.Equal(timestamp, message.Timestamp);
        Assert.Equal("DUS-001", message.SensorCode);
        Assert.Equal("Dust", message.SensorType);
        Assert.Equal(1, message.TextileId);
        Assert.Equal(22.5, message.Temperature);
        Assert.Equal(55.0, message.Humidity);
        Assert.Equal(2.5, message.FrassDensity);
        Assert.Equal(3, message.HoleCount);
        Assert.Equal(35.0, message.PM2_5);
        Assert.Equal(60.0, message.PM10);
        Assert.Equal(150.0, message.SporeCount);
        Assert.Equal(180.0, message.FungiCFU);
        Assert.Equal("Aspergillus", message.DominantFungiType);
        Assert.Equal(-45, message.SignalStrength);
    }

    [Fact]
    public void QueueNames_Constants_AreCorrect()
    {
        Assert.Equal("textile.sensor_data", QueueNames.SensorData);
        Assert.Equal("textile.population_prediction", QueueNames.PopulationPrediction);
        Assert.Equal("textile.mildew_prediction", QueueNames.MildewPrediction);
        Assert.Equal("textile.alert_trigger", QueueNames.AlertTrigger);
        Assert.Equal("textile.alert_dispatch", QueueNames.AlertDispatch);
    }

    [Fact]
    public void QueueNames_Exchanges_AreCorrect()
    {
        Assert.Equal("textile.exchange.sensor", QueueNames.Exchanges.Sensor);
        Assert.Equal("textile.exchange.prediction", QueueNames.Exchanges.Prediction);
        Assert.Equal("textile.exchange.alert", QueueNames.Exchanges.Alert);
    }

    [Fact]
    public void QueueNames_RoutingKeys_AreCorrect()
    {
        Assert.Equal("sensor.dust", QueueNames.RoutingKeys.DustSensor);
        Assert.Equal("sensor.fungi", QueueNames.RoutingKeys.FungiSensor);
        Assert.Equal("prediction.population", QueueNames.RoutingKeys.Population);
        Assert.Equal("prediction.mildew", QueueNames.RoutingKeys.Mildew);
        Assert.Equal("prediction.synergy", QueueNames.RoutingKeys.Synergy);
    }

    [Fact]
    public void PopulationPredictionGenerated_Properties_CanBeSet()
    {
        var message = new PopulationPredictionGenerated
        {
            CorrelationId = Guid.NewGuid(),
            TextileId = 1,
            HorizonDays = 30,
            FinalPredictedDensity = 5.2345,
            RiskLevel = 2,
            Confidence = 0.85,
            GeneratedAt = DateTime.UtcNow
        };

        Assert.Equal(1, message.TextileId);
        Assert.Equal(30, message.HorizonDays);
        Assert.Equal(5.2345, message.FinalPredictedDensity);
        Assert.Equal(2, message.RiskLevel);
        Assert.Equal(0.85, message.Confidence);
        Assert.NotNull(message.DataPoints);
    }

    [Fact]
    public void MildewPredictionGenerated_Properties_CanBeSet()
    {
        var message = new MildewPredictionGenerated
        {
            CorrelationId = Guid.NewGuid(),
            TextileId = 1,
            HorizonDays = 30,
            FinalPredictedCFU = 350.5,
            InflectionPointDay = 12,
            DoublingTimeHours = 48.5,
            RiskLevel = 2,
            Confidence = 0.82,
            GeneratedAt = DateTime.UtcNow
        };

        Assert.Equal(1, message.TextileId);
        Assert.Equal(30, message.HorizonDays);
        Assert.Equal(350.5, message.FinalPredictedCFU);
        Assert.Equal(12, message.InflectionPointDay);
        Assert.Equal(48.5, message.DoublingTimeHours);
        Assert.Equal(2, message.RiskLevel);
        Assert.Equal(0.82, message.Confidence);
    }

    [Fact]
    public void AlertTriggered_Properties_CanBeSet()
    {
        var message = new AlertTriggered
        {
            CorrelationId = Guid.NewGuid(),
            TextileId = 1,
            AlertType = 1,
            AlertLevel = 2,
            Title = "Test Alert",
            Message = "Test message",
            ThresholdValue = 5.0,
            ActualValue = 6.5,
            TriggeredAt = DateTime.UtcNow,
            Severity = "High"
        };

        Assert.Equal(1, message.TextileId);
        Assert.Equal(1, message.AlertType);
        Assert.Equal(2, message.AlertLevel);
        Assert.Equal("Test Alert", message.Title);
        Assert.Equal("Test message", message.Message);
        Assert.Equal(5.0, message.ThresholdValue);
        Assert.Equal(6.5, message.ActualValue);
        Assert.Equal("High", message.Severity);
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(25, 100, 25)]
    [InlineData(50, 100, 50)]
    [InlineData(75, 100, 75)]
    [InlineData(100, 100, 100)]
    public void SensorDataReceived_WithDifferentValues_WorksCorrectly(double temp, double hum, int holeCount)
    {
        var message = new SensorDataReceived
        {
            TextileId = 1,
            Temperature = temp,
            Humidity = hum,
            HoleCount = holeCount
        };

        Assert.Equal(temp, message.Temperature);
        Assert.Equal(hum, message.Humidity);
        Assert.Equal(holeCount, message.HoleCount);
    }
}
