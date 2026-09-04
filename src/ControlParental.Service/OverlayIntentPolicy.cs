namespace ControlParental.Service;

using ControlParental.Domain;

public sealed class OverlayIntentPolicy
{
    private readonly IOverlayIntentStore store;
    private long deliveredVersion = -1;
    private long deliveredGeneration = -1;
    private bool deliveredDesired;

    public OverlayIntentPolicy(IOverlayIntentStore store)
    {
        this.store = store;
        this.Intent = store.Load() ?? new(false, string.Empty, null, 0);
    }

    public OverlayIntent Intent { get; private set; }

    public void SetDesired(bool desired, string reason, string? ctaLabel = null)
    {
        if (this.Intent.Desired == desired && this.Intent.Reason == reason && this.Intent.CtaLabel == ctaLabel)
        {
            return;
        }

        this.Intent = new(desired, reason, ctaLabel, checked(this.Intent.Version + 1));
        this.store.Save(this.Intent);
    }

    public async ValueTask<ActionStatus> ReconcileAsync(int sessionId, IAgentCommandPort? port, CancellationToken token = default)
    {
        if (port is null)
        {
            return ActionStatus.ConnectionReplaced;
        }

        var intent = this.Intent;
        var generation = port.Generation;
        if (this.deliveredVersion == intent.Version && this.deliveredGeneration == generation)
        {
            return ActionStatus.HarmlessAbsence;
        }

        var kind = !intent.Desired
            ? AgentCommandKind.ClearOverlay
            : this.deliveredGeneration == generation && this.deliveredVersion >= 0 && this.deliveredDesired
                ? AgentCommandKind.ReplaceOverlay
                : AgentCommandKind.ShowOverlay;
        var command = new AgentCommandEnvelope(
            Guid.NewGuid(), sessionId, generation, intent.Version,
            kind, DateTimeOffset.UtcNow.AddSeconds(3), intent);
        var result = await port.ExecuteAsync(command, token);
        if (this.Intent.Version != intent.Version)
        {
            return ActionStatus.Stale;
        }

        if (result.Status == ActionStatus.Confirmed)
        {
            this.deliveredVersion = intent.Version;
            this.deliveredGeneration = generation;
            this.deliveredDesired = intent.Desired;
        }

        return result.Status;
    }
}
