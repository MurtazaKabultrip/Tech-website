namespace Novaforge.Web.Services;

/// <summary>
/// In-process change feed so open portal pages update live (new message, status change, invoice paid)
/// without polling. For multiple app instances, replace with Redis pub/sub or Azure SignalR.
/// </summary>
public sealed class ProjectNotifier
{
    public event Func<Guid, Task>? ProjectChanged;

    public async Task NotifyAsync(Guid projectId)
    {
        var handlers = ProjectChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<Guid, Task>>())
        {
            try
            {
                await handler(projectId);
            }
            catch (ObjectDisposedException)
            {
                // A circuit went away mid-notification; it unsubscribes on dispose.
            }
        }
    }
}
