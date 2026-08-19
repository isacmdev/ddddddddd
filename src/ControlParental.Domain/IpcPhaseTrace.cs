namespace ControlParental.Domain;

/// <summary>Bounded, secret-free observability for the authenticated transport.</summary>
public enum IpcPhase
{
    Connected,
    ServerPid,
    ProcessHandle,
    Path,
    AuthStart,
    AuthResult,
    SidSession,
    ClientHelloWrite,
    ClientHelloRead,
    ServerHelloWrite,
    ServerHelloRead,
    Authenticated,
    Dispatch,
}

public sealed class IpcPhaseTrace
{
    private readonly List<IpcPhase> phases = new();

    public IReadOnlyList<IpcPhase> Phases => this.phases;

    public IpcPhase? CancelledPhase { get; private set; }

    public void Record(IpcPhase phase)
    {
        this.phases.Add(phase);
        System.Diagnostics.Debug.WriteLine($"[IPC] phase={phase}");
    }

    public void CancelledAt(IpcPhase phase)
    {
        this.phases.Add(phase);
        this.CancelledPhase = phase;
        System.Diagnostics.Debug.WriteLine($"[IPC] cancelled-at-phase={phase}");
    }
}
