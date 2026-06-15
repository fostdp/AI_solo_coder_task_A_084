namespace TextileMonitoring.AlertDispatch.Services;

public interface IDingTalkNotifier
{
    Task<bool> SendAsync(string title, string content, CancellationToken ct = default);
}
