using TextileMonitoring.Contracts.Messages;

namespace TextileMonitoring.AlertDispatch.Services;

public interface IAlertDispatchService
{
    Task DispatchAsync(AlertTriggered alert, CancellationToken ct = default);
}
