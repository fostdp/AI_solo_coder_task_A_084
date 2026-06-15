
using AlertDispatch.Services;
using AlertDispatch.Models;
using TextileMonitoring.Contracts.Messages;

namespace TextileMonitoring.Tests.Services;

public class AlertDispatchTests
{
    private readonly AlertDispatchService _dispatchService;
    private readonly Mock<IDingTalkNotifier> _dingTalkMock;
    private readonly Mock<IEmailNotifier> _emailMock;
    private readonly Mock<IAlertRepository> _repositoryMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<AlertDispatchService>> _loggerMock;

    public AlertDispatchTests()
    {
        _dingTalkMock = new Mock<IDingTalkNotifier>();
        _emailMock = new Mock<IEmailNotifier>();
        _repositoryMock = new Mock<IAlertRepository>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<AlertDispatchService>>();

        var options = Microsoft.Extensions.Options.Options.Create(new AlertDispatchOptions
        {
            CriticalChannels = "dingtalk,email",
            HighChannels = "dingtalk",
            MediumChannels = "email",
            LowChannels = "",
            DingTalkWebhook = "https://oapi.dingtalk.com/robot/send?access_token=test",
            DingTalkSecret = "test_secret",
            SmtpHost = "smtp.test.com",
            SmtpPort = 587,
            SmtpUser = "alerts@test.com",
            SmtpPassword = "password",
            SmtpEnableSsl = true,
            SmtpFromName = "测试系统",
            EmailRecipients = "admin@test.com"
        });

        _dispatchService = new AlertDispatchService(
            _loggerMock.Object,
            options,
            _dingTalkMock.Object,
            _emailMock.Object,
            _repositoryMock.Object);
    }

    [Theory]
    [InlineData(4, true, true)]
    [InlineData(3, true, false)]
    [InlineData(2, false, true)]
    [InlineData(1, false, false)]
    public void AlertDispatch_RoutingBySeverity_IsCorrect(
        int alertLevel, bool shouldDingTalk, bool shouldEmail)
    {
        var alert = new AlertTriggered
        {
            TextileId = 1,
            AlertLevel = alertLevel,
            AlertType = 1,
            Title = "Test Alert",
            Message = "Test message",
            ThresholdValue = 5.0,
            ActualValue = 6.5
        };

        var config = new AlertDispatchOptions
        {
            CriticalChannels = "dingtalk,email",
            HighChannels = "dingtalk",
            MediumChannels = "email",
            LowChannels = ""
        };

        var channels = _dispatchService.GetChannelsForSeverity(alertLevel, config);

        Assert.Equal(shouldDingTalk, channels.Contains("dingtalk"));
        Assert.Equal(shouldEmail, channels.Contains("email"));
    }

    [Fact]
    public void AlertDispatch_SeverityName_IsCorrect()
    {
        Assert.Equal("Critical", _dispatchService.GetSeverityName(4));
        Assert.Equal("High", _dispatchService.GetSeverityName(3));
        Assert.Equal("Medium", _dispatchService.GetSeverityName(2));
        Assert.Equal("Low", _dispatchService.GetSeverityName(1));
        Assert.Equal("Unknown", _dispatchService.GetSeverityName(0));
    }

    [Fact]
    public void AlertDispatch_AlertTypeName_IsCorrect()
    {
        Assert.Equal("Hole Density", _dispatchService.GetAlertTypeName(1));
        Assert.Equal("Fungi CFU", _dispatchService.GetAlertTypeName(2));
        Assert.Equal("Synergy Risk", _dispatchService.GetAlertTypeName(3));
        Assert.Equal("Unknown", _dispatchService.GetAlertTypeName(0));
    }

    [Fact]
    public async Task AlertDispatchService_DispatchesCriticalAlert_ToBothChannels()
    {
        var alert = new AlertTriggered
        {
            CorrelationId = Guid.NewGuid(),
            TextileId = 1,
            AlertLevel = 4,
            AlertType = 1,
            Title = "严重告警",
            Message = "虫蛀密度严重超标",
            ThresholdValue = 5.0,
            ActualValue = 8.5,
            TriggeredAt = DateTime.UtcNow,
            Severity = "Critical"
        };

        _dingTalkMock.Setup(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _emailMock.Setup(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _dispatchService.DispatchAsync(alert);

        Assert.NotNull(result);
        Assert.True(result.DingTalkPushed);
        Assert.True(result.EmailPushed);
        _dingTalkMock.Verify(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailMock.Verify(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlertDispatchService_DispatchesHighAlert_ToDingTalkOnly()
    {
        var alert = new AlertTriggered
        {
            CorrelationId = Guid.NewGuid(),
            TextileId = 1,
            AlertLevel = 3,
            AlertType = 2,
            Title = "高优先级告警",
            Message = "霉菌浓度超标",
            ThresholdValue = 300.0,
            ActualValue = 380.0,
            TriggeredAt = DateTime.UtcNow,
            Severity = "High"
        };

        _dingTalkMock.Setup(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _emailMock.Setup(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _dispatchService.DispatchAsync(alert);

        Assert.NotNull(result);
        Assert.True(result.DingTalkPushed);
        Assert.False(result.EmailPushed);
        _dingTalkMock.Verify(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailMock.Verify(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AlertDispatchService_DispatchesMediumAlert_ToEmailOnly()
    {
        var alert = new AlertTriggered
        {
            CorrelationId = Guid.NewGuid(),
            TextileId = 1,
            AlertLevel = 2,
            AlertType = 3,
            Title = "中等优先级告警",
            Message = "协同风险超标",
            ThresholdValue = 50.0,
            ActualValue = 55.0,
            TriggeredAt = DateTime.UtcNow,
            Severity = "Medium"
        };

        _dingTalkMock.Setup(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _emailMock.Setup(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _dispatchService.DispatchAsync(alert);

        Assert.NotNull(result);
        Assert.False(result.DingTalkPushed);
        Assert.True(result.EmailPushed);
        _dingTalkMock.Verify(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailMock.Verify(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlertDispatchService_LowAlert_NoNotification()
    {
        var alert = new AlertTriggered
        {
            CorrelationId = Guid.NewGuid(),
            TextileId = 1,
            AlertLevel = 1,
            AlertType = 1,
            Title = "低优先级通知",
            Message = "虫蛀密度轻微上升",
            ThresholdValue = 3.0,
            ActualValue = 3.2,
            TriggeredAt = DateTime.UtcNow,
            Severity = "Low"
        };

        var result = await _dispatchService.DispatchAsync(alert);

        Assert.NotNull(result);
        Assert.False(result.DingTalkPushed);
        Assert.False(result.EmailPushed);
        _dingTalkMock.Verify(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailMock.Verify(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AlertDispatchService_WithRetry_OnFailure()
    {
        var alert = new AlertTriggered
        {
            CorrelationId = Guid.NewGuid(),
            TextileId = 1,
            AlertLevel = 4,
            AlertType = 1,
            Title = "严重告警",
            Message = "测试重试机制",
            ThresholdValue = 5.0,
            ActualValue = 8.5,
            TriggeredAt = DateTime.UtcNow,
            Severity = "Critical"
        };

        var callCount = 0;
        _dingTalkMock.Setup(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount >= 2;
            });
        _emailMock.Setup(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _dispatchService.DispatchAsync(alert);

        Assert.NotNull(result);
        Assert.True(result.DingTalkPushed);
        _dingTalkMock.Verify(x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void DingTalkNotifier_Signature_IsGeneratedCorrectly()
    {
        var notifier = new DingTalkNotifier(
            Microsoft.Extensions.Options.Options.Create(new AlertDispatchOptions
            {
                DingTalkWebhook = "https://oapi.dingtalk.com/robot/send?access_token=test",
                DingTalkSecret = "SEC1234567890"
            }),
            new Mock<Microsoft.Extensions.Logging.ILogger<DingTalkNotifier>>().Object);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var signature = notifier.GenerateSignature(timestamp, "SEC1234567890");

        Assert.NotNull(signature);
        Assert.NotEmpty(signature);
        Assert.Contains("%", signature);
    }

    [Fact]
    public void AlertDispatchService_FormatsMessage_Correctly()
    {
        var alert = new AlertTriggered
        {
            TextileId = 1,
            AlertLevel = 4,
            AlertType = 1,
            Title = "测试告警",
            Message = "测试消息内容",
            ThresholdValue = 5.0,
            ActualValue = 8.5,
            TriggeredAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        var (title, content) = _dispatchService.FormatAlertMessage(alert);

        Assert.Contains("Critical", title);
        Assert.Contains("测试告警", title);
        Assert.Contains("8.5", content);
        Assert.Contains("5.0", content);
        Assert.Contains("2025", content);
    }

    [Theory]
    [InlineData(5.0, 8.5, "超标")]
    [InlineData(5.0, 4.5, "未超标")]
    [InlineData(300.0, 350.0, "超标")]
    public void AlertDispatchService_StatusText_IsCorrect(double threshold, double actual, string expected)
    {
        var result = _dispatchService.GetStatusText(actual, threshold);
        Assert.Contains(expected, result);
    }

    [Fact]
    public async Task AlertConsumer_ProcessesAlertMessage()
    {
        var consumer = new AlertDispatch.Consumers.AlertTriggeredConsumer(
            _dispatchService,
            new Mock<Microsoft.Extensions.Logging.ILogger<AlertDispatch.Consumers.AlertTriggeredConsumer>>().Object);

        var context = new Mock<MassTransit.ConsumeContext<AlertTriggered>>();
        context.SetupGet(x => x.Message).Returns(new AlertTriggered
        {
            CorrelationId = Guid.NewGuid(),
            TextileId = 1,
            AlertLevel = 2,
            AlertType = 1,
            Title = "测试",
            Message = "测试",
            ThresholdValue = 5.0,
            ActualValue = 6.0
        });

        _repositoryMock.Setup(x => x.SaveDispatchResultAsync(It.IsAny<TextileMonitoring.Data.Entities.Alert>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        _repositoryMock.Verify(x => x.SaveDispatchResultAsync(It.IsAny<TextileMonitoring.Data.Entities.Alert>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
