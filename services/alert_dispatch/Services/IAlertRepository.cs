using TextileMonitoring.Contracts.Messages;
using TextileMonitoring.Data.Entities;

namespace TextileMonitoring.AlertDispatch.Services;

public interface IAlertRepository
{
    Task<Alert> CreateAlertAsync(AlertTriggered alertTriggered, CancellationToken ct = default);
    Task<Alert?> GetByIdAsync(int id, CancellationToken ct = default);
    Task UpdateAsync(Alert alert, CancellationToken ct = default);
}
