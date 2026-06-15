using MassTransit;
using Serilog;
using TextileMonitoring.AlertDispatch.Services;
using TextileMonitoring.Contracts.Messages;

namespace TextileMonitoring.AlertDispatch.Consumers;

public class AlertTriggeredConsumer : IConsumer<AlertTriggered>
{
    private readonly IAlertDispatchService _dispatchService;
    private readonly ILogger _logger;

    public AlertTriggeredConsumer(IAlertDispatchService dispatchService, ILogger logger)
    {
        _dispatchService = dispatchService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AlertTriggered> context)
    {
        var alert = context.Message;

        _logger.Information("Received AlertTriggered event: {CorrelationId}, Level: {AlertLevel}, Textile: {TextileName}",
            alert.CorrelationId, alert.AlertLevel, alert.TextileName);

        try
        {
            await _dispatchService.DispatchAsync(alert, context.CancellationToken);
            _logger.Information("Successfully processed alert: {CorrelationId}", alert.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process alert: {CorrelationId}", alert.CorrelationId);
            throw;
        }
    }
}

public class AlertTriggeredConsumerDefinition : ConsumerDefinition<AlertTriggeredConsumer>
{
    private const string ServiceName = "alert-dispatch";
    private const string EventName = "alert-triggered";

    public AlertTriggeredConsumerDefinition()
    {
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<AlertTriggeredConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Immediate(3));
        endpointConfigurator.UseDeadLetterQueue($"textile-monitoring.{ServiceName}.{EventName}.dlq");

        endpointConfigurator.ConfigureConsumer<AlertTriggeredConsumer>(context);
    }
}
