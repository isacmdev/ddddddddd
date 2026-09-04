namespace ControlParental.Domain.Tests;

using ControlParental.Domain;
using System.Text.Json;
using Xunit;

public sealed class ContractFreezeAcceptanceTests
{
    private static string Fixtures => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "openspec", "changes", "shared-contracts-freeze", "fixtures"));

    [Fact] public void CT01_AllValidFixturesHaveStrictEnvelope() => AssertValid("valid-set-protected-account.json", "valid-policy-snapshot.json", "valid-create-time-request.json");
    [Fact] public void CT02_EveryNegativeHasOneRejectingCause()
    {
        foreach (var file in Directory.GetFiles(Fixtures, "invalid-*.json"))
        {
            Assert.True(Reject(file).Count > 0, $"negative fixture was accepted: {Path.GetFileName(file)}");
        }
    }
    [Fact] public void CT03_AccountContractIsBounded() => AssertValid("valid-set-protected-account.json", "valid-set-protected-account-response.json");
    [Fact] public void CT04_ActivationStatesAreFailClosed() => AssertValid("valid-runtime-activation.json");
    [Fact] public void CT05_OutboxContractIsTruthful() => AssertValid("valid-create-time-request.json", "valid-time-request-outbox.json");
    [Fact] public void CT06_GrantsRequireEvidence() => AssertValid("valid-policy-snapshot.json");
    [Fact] public void CT07_IdentityContractIsBound() => AssertValid("valid-create-time-request.json");
    [Fact] public void CT08_DeviceIsolationContractIsDeclared() => AssertValid("valid-policy-snapshot.json");
    [Fact] public void CT09_PolicyVersionIsMonotonic() => AssertValid("valid-policy-snapshot.json");
    [Fact] public void CT10_IntegrityVerdictIsAuthoritative() => AssertValid("valid-integrity-evidence.json", "valid-integrity-verdict.json");
    [Fact] public void CT11_HintsAreSignalOnlyAndBounded() { AssertValid("valid-wns-hint.json", "valid-realtime-hint.json", "valid-hint-1024.json"); Assert.NotEmpty(Reject(Path.Combine(Fixtures, "invalid-hint-1025.json"))); Assert.NotEmpty(Reject(Path.Combine(Fixtures, "invalid-hint-member.json"))); }
    [Fact] public void CT12_ReceiptsAreSecretFree() { foreach (var file in Directory.GetFiles(Fixtures, "valid-*.json")) Assert.DoesNotContain("token", File.ReadAllText(file), StringComparison.OrdinalIgnoreCase); }

    private static void AssertValid(params string[] names)
    {
        foreach (var name in names) Assert.Empty(Reject(Path.Combine(Fixtures, name)));
    }

    private static IReadOnlyList<string> Reject(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return ContractFreezeValidator.ValidateMessage(document.RootElement, checked((int)new FileInfo(path).Length));
    }
}
