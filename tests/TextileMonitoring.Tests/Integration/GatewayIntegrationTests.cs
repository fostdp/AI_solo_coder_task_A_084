
using MassTransit;
using TextileMonitoring.API.Services;
using TextileMonitoring.Contracts.Messages;
using TextileMonitoring.Contracts.RabbitMQ;
using TextileMonitoring.Data.Repositories;

namespace TextileMonitoring.Tests.Integration;

public class GatewayIntegrationTests : TestBase
{
    [Fact]
    public async Task GatewayService_CanPublishSensorData()
    {
        using var context = CreateDbContext(nameof(GatewayService_CanPublishSensorData));

        var busMock = new Mock<IBus>();
        var textileRepo = new TextileRepository(context);
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<PredictionGatewayService>>();
        var popClientMock = new Mock<IRequestClient<SensorDataReceived, PopulationPredictionGenerated>>();
        var mildewClientMock = new Mock<IRequestClient<SensorDataReceived, MildewPredictionGenerated>>();

        var gatewayService = new PredictionGatewayService(
            busMock.Object,
            textileRepo,
            loggerMock.Object,
            popClientMock.Object,
            mildewClientMock.Object);

        var sensorData = new SensorDataReceived
        {
            TextileId = 1,
            SensorCode = "DUS-001",
            SensorType = "Dust",
            Temperature = 22.5,
            Humidity = 55.0,
            FrassDensity = 1.5,
            HoleCount = 3
        };

        await gatewayService.PublishSensorDataAsync(sensorData);

        busMock.Verify(x => x.Publish(sensorData, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GatewayService_GetPopulationPredictionAsync_ReturnsResult()
    {
        using var context = CreateDbContext(nameof(GatewayService_GetPopulationPredictionAsync_ReturnsResult));

        var textile = new TextileMonitoring.Data.Entities.Textile
        {
            Id = 1,
            Name = "Test Textile",
            Dynasty = "明",
            Material = "云锦",
            WidthCm = 80,
            HeightCm = 120,
            Location = "A区",
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Textiles.Add(textile);
        await context.SaveChangesAsync();

        var busMock = new Mock<IBus>();
        var textileRepo = new TextileRepository(context);
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<PredictionGatewayService>>();

        var expectedResult = new PopulationPredictionGenerated
        {
            TextileId = 1,
            HorizonDays = 30,
            FinalPredictedDensity = 3.5,
            RiskLevel = 1,
            Confidence = 0.85
        };

        var popClientMock = new Mock<IRequestClient<SensorDataReceived, PopulationPredictionGenerated>>();
        popClientMock.Setup(x => x.GetResponse<PopulationPredictionGenerated>(
                It.IsAny<SensorDataReceived>(), It.IsAny<CancellationToken>(), It.IsAny<RequestTimeout>()))
            .ReturnsAsync(new MockResponse<PopulationPredictionGenerated>(expectedResult));

        var mildewClientMock = new Mock<IRequestClient<SensorDataReceived, MildewPredictionGenerated>>();

        var gatewayService = new PredictionGatewayService(
            busMock.Object,
            textileRepo,
            loggerMock.Object,
            popClientMock.Object,
            mildewClientMock.Object);

        var result = await gatewayService.GetPopulationPredictionAsync(1, 30);

        Assert.NotNull(result);
        Assert.Equal(1, result.TextileId);
        Assert.Equal(30, result.HorizonDays);
        Assert.Equal(3.5, result.FinalPredictedDensity);
    }

    [Fact]
    public async Task GatewayService_GetMildewPredictionAsync_ReturnsResult()
    {
        using var context = CreateDbContext(nameof(GatewayService_GetMildewPredictionAsync_ReturnsResult));

        var textile = new TextileMonitoring.Data.Entities.Textile
        {
            Id = 1,
            Name = "Test Textile",
            Dynasty = "明",
            Material = "云锦",
            WidthCm = 80,
            HeightCm = 120,
            Location = "A区",
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Textiles.Add(textile);
        await context.SaveChangesAsync();

        var busMock = new Mock<IBus>();
        var textileRepo = new TextileRepository(context);
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<PredictionGatewayService>>();
        var popClientMock = new Mock<IRequestClient<SensorDataReceived, PopulationPredictionGenerated>>();

        var expectedResult = new MildewPredictionGenerated
        {
            TextileId = 1,
            HorizonDays = 30,
            FinalPredictedCFU = 350.5,
            InflectionPointDay = 12,
            DoublingTimeHours = 48.5,
            RiskLevel = 2,
            Confidence = 0.82
        };

        var mildewClientMock = new Mock<IRequestClient<SensorDataReceived, MildewPredictionGenerated>>();
        mildewClientMock.Setup(x => x.GetResponse<MildewPredictionGenerated>(
                It.IsAny<SensorDataReceived>(), It.IsAny<CancellationToken>(), It.IsAny<RequestTimeout>()))
            .ReturnsAsync(new MockResponse<MildewPredictionGenerated>(expectedResult));

        var gatewayService = new PredictionGatewayService(
            busMock.Object,
            textileRepo,
            loggerMock.Object,
            popClientMock.Object,
            mildewClientMock.Object);

        var result = await gatewayService.GetMildewPredictionAsync(1, 30);

        Assert.NotNull(result);
        Assert.Equal(1, result.TextileId);
        Assert.Equal(30, result.HorizonDays);
        Assert.Equal(350.5, result.FinalPredictedCFU);
    }

    [Fact]
    public async Task GatewayService_GetCombinedPredictionAsync_ReturnsBothResults()
    {
        using var context = CreateDbContext(nameof(GatewayService_GetCombinedPredictionAsync_ReturnsBothResults));

        var textile = new TextileMonitoring.Data.Entities.Textile
        {
            Id = 1,
            Name = "Test Textile",
            Dynasty = "明",
            Material = "云锦",
            WidthCm = 80,
            HeightCm = 120,
            Location = "A区",
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Textiles.Add(textile);
        await context.SaveChangesAsync();

        var busMock = new Mock<IBus>();
        var textileRepo = new TextileRepository(context);
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<PredictionGatewayService>>();

        var popResult = new PopulationPredictionGenerated
        {
            TextileId = 1,
            HorizonDays = 30,
            FinalPredictedDensity = 3.5,
            RiskLevel = 1,
            Confidence = 0.85
        };

        var mildewResult = new MildewPredictionGenerated
        {
            TextileId = 1,
            HorizonDays = 30,
            FinalPredictedCFU = 350.5,
            RiskLevel = 2,
            Confidence = 0.82
        };

        var popClientMock = new Mock<IRequestClient<SensorDataReceived, PopulationPredictionGenerated>>();
        popClientMock.Setup(x => x.GetResponse<PopulationPredictionGenerated>(
                It.IsAny<SensorDataReceived>(), It.IsAny<CancellationToken>(), It.IsAny<RequestTimeout>()))
            .ReturnsAsync(new MockResponse<PopulationPredictionGenerated>(popResult));

        var mildewClientMock = new Mock<IRequestClient<SensorDataReceived, MildewPredictionGenerated>>();
        mildewClientMock.Setup(x => x.GetResponse<MildewPredictionGenerated>(
                It.IsAny<SensorDataReceived>(), It.IsAny<CancellationToken>(), It.IsAny<RequestTimeout>()))
            .ReturnsAsync(new MockResponse<MildewPredictionGenerated>(mildewResult));

        var gatewayService = new PredictionGatewayService(
            busMock.Object,
            textileRepo,
            loggerMock.Object,
            popClientMock.Object,
            mildewClientMock.Object);

        var (pop, mildew) = await gatewayService.GetCombinedPredictionAsync(1, 30);

        Assert.NotNull(pop);
        Assert.NotNull(mildew);
        Assert.Equal(3.5, pop.FinalPredictedDensity);
        Assert.Equal(350.5, mildew.FinalPredictedCFU);
    }

    [Fact]
    public async Task GatewayService_GetPopulationPredictionAsync_ReturnsNullForNotFound()
    {
        using var context = CreateDbContext(nameof(GatewayService_GetPopulationPredictionAsync_ReturnsNullForNotFound));

        var busMock = new Mock<IBus>();
        var textileRepo = new TextileRepository(context);
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<PredictionGatewayService>>();
        var popClientMock = new Mock<IRequestClient<SensorDataReceived, PopulationPredictionGenerated>>();
        var mildewClientMock = new Mock<IRequestClient<SensorDataReceived, MildewPredictionGenerated>>();

        var gatewayService = new PredictionGatewayService(
            busMock.Object,
            textileRepo,
            loggerMock.Object,
            popClientMock.Object,
            mildewClientMock.Object);

        var result = await gatewayService.GetPopulationPredictionAsync(999, 30);

        Assert.Null(result);
        popClientMock.Verify(x => x.GetResponse<PopulationPredictionGenerated>(
            It.IsAny<SensorDataReceived>(), It.IsAny<CancellationToken>(), It.IsAny<RequestTimeout>()), Times.Never);
    }

    [Fact]
    public void QueueNames_AreCorrectlyConfigured()
    {
        Assert.Equal("textile.sensor_data", QueueNames.SensorData);
        Assert.Equal("textile.exchange.sensor", QueueNames.Exchanges.Sensor);
        Assert.Equal("textile.exchange.prediction", QueueNames.Exchanges.Prediction);
        Assert.Equal("textile.exchange.alert", QueueNames.Exchanges.Alert);
    }
}

public class MockResponse<T> : Response<T>
    where T : class
{
    public T Message { get; }
    public Guid? RequestId { get; }
    public Guid? MessageId { get; }
    public Guid? CorrelationId { get; }
    public Guid? ConversationId { get; }
    public Guid? InitiatorId { get; }
    public DateTime? ExpirationTime { get; }
    public DateTime? SentTime { get; }
    public HostInfo Host { get; }
    public Uri SourceAddress { get; }
    public Uri DestinationAddress { get; }
    public Uri ResponseAddress { get; }
    public Uri FaultAddress { get; }

    public MockResponse(T message)
    {
        Message = message;
        RequestId = Guid.NewGuid();
        MessageId = Guid.NewGuid();
        CorrelationId = Guid.NewGuid();
        ConversationId = Guid.NewGuid();
        InitiatorId = Guid.NewGuid();
        SentTime = DateTime.UtcNow;
        Host = new MockHostInfo();
        SourceAddress = new Uri("rabbitmq://localhost/test");
        DestinationAddress = new Uri("rabbitmq://localhost/test");
    }

    public TResponse MessageTo<TResponse>()
        where TResponse : class
    {
        throw new NotImplementedException();
    }

    public bool IsResponseType<TResponse>()
        where TResponse : class
    {
        return typeof(TResponse) == typeof(T);
    }
}

public class MockHostInfo : HostInfo
{
    public string MachineName { get; } = "TestMachine";
    public string ProcessName { get; } = "TestProcess";
    public int ProcessId { get; } = 1234;
    public string Assembly { get; } = "TestAssembly";
    public string AssemblyVersion { get; } = "1.0.0.0";
    public string FrameworkVersion { get; } = "8.0.0";
    public string MassTransitVersion { get; } = "8.2.0.0";
    public string OperatingSystemVersion { get; } = "Windows 10";
    public DateTime? StartTime { get; } = DateTime.UtcNow;
}
