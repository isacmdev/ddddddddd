namespace ControlParental.Service;

using System.Diagnostics;

public sealed record ProcessIdentity(
    int ProcessId, int SessionId, DateTimeOffset StartedAt, string ProcessName);

public interface IExactProcessHandle : IDisposable
{
    bool HasExited { get; }
    ProcessIdentity ReadIdentity();
    bool CloseMainWindow();
    void Kill();
    Task WaitForExitAsync(CancellationToken cancellationToken);
}

internal sealed class SystemProcessHandle(Process process) : IExactProcessHandle
{
    public bool HasExited => process.HasExited;

    public ProcessIdentity ReadIdentity() => new(
        process.Id,
        process.SessionId,
        process.StartTime.ToUniversalTime(),
        process.ProcessName);

    public bool CloseMainWindow() => process.CloseMainWindow();
    public void Kill() => process.Kill(entireProcessTree: true);
    public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);
    public void Dispose() => process.Dispose();
}
