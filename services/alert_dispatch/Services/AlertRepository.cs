using Microsoft.EntityFrameworkCore;
using TextileMonitoring.Contracts.Messages;
using TextileMonitoring.Data;
using TextileMonitoring.Data.Entities;

namespace TextileMonitoring.AlertDispatch.Services;

public class AlertRepository : IAlertRepository
{
    private readonly TextileMonitoringDbContext _context;

    public AlertRepository(TextileMonitoringDbContext context)
    {
        _context = context;
    }

    public async Task<Alert> CreateAlertAsync(AlertTriggered alertTriggered, CancellationToken ct = default)
    {
        var alertLevel = MapAlertLevel(alertTriggered.AlertLevel);
        var alertType = MapAlertType(alertTriggered.AlertType);

        var alert = new Alert
        {
            TextileId = alertTriggered.TextileId,
            AlertType = alertType,
            AlertLevel = alertLevel,
            Title = alertTriggered.Title,
            Description = alertTriggered.Description,
            ActualValue = (decimal)alertTriggered.ActualValue,
            Threshold = (decimal)alertTriggered.Threshold,
            Resolved = false,
            CreatedAt = alertTriggered.Timestamp
        };

        _context.Alerts.Add(alert);
        await _context.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<Alert?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Alerts.FindAsync(new object[] { id }, ct);
    }

    public async Task UpdateAsync(Alert alert, CancellationToken ct = default)
    {
        _context.Alerts.Update(alert);
        await _context.SaveChangesAsync(ct);
    }

    private static int MapAlertLevel(string alertLevel)
    {
        return alertLevel.ToLower() switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 2
        };
    }

    private static int MapAlertType(string alertType)
    {
        return alertType.ToLower() switch
        {
            "dust" => 1,
            "fungi" => 2,
            "prediction" => 3,
            "hole" => 4,
            "mold" => 5,
            _ => 0
        };
    }
}
