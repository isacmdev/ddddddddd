namespace ControlParental.Domain;

public interface IAuthoritativeHealthSink
{
    void SetRestoreStatus(bool succeeded);

    void SetCurrentCriticalActionsConfirmed(bool confirmed);

    void SetHealthBlockingIssues(bool hasBlockingIssues);
}
