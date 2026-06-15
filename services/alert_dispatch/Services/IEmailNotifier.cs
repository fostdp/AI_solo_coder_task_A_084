namespace TextileMonitoring.AlertDispatch.Services;

public interface IEmailNotifier
{
    Task<bool> SendAsync(string subject, string body, IEnumerable<string> recipients, CancellationToken ct = default);
}
