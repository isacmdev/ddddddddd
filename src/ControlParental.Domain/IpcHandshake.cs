namespace ControlParental.Domain;

using System.Text.Json;

/// <summary>Authenticated first frame for a Service/SessionAgent connection.</summary>
public sealed record IpcHandshake(int SessionId, int ProcessId, string Signer)
{
    public const string MessageType = "IpcHandshake";

    public static string Create(int sessionId, int processId, string signer)
        => JsonSerializer.Serialize(new { MessageType, SessionId = sessionId, ProcessId = processId, Signer = signer });

    public static bool TryParse(string json, out IpcHandshake handshake)
    {
        handshake = new IpcHandshake(0, 0, string.Empty);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("MessageType").GetString() != MessageType)
            {
                return false;
            }

            var signer = root.GetProperty("Signer").GetString();
            if (signer is null or { Length: 0 })
            {
                return false;
            }

            handshake = new IpcHandshake(
                root.GetProperty("SessionId").GetInt32(),
                root.GetProperty("ProcessId").GetInt32(),
                signer);
            return handshake.SessionId >= 0 && handshake.ProcessId > 0;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    public static bool IsAuthorized(IpcHandshake handshake, int sessionId, int processId, string signer)
        => handshake.SessionId == sessionId && handshake.ProcessId == processId &&
           !string.IsNullOrWhiteSpace(signer) &&
           string.Equals(handshake.Signer, signer, StringComparison.OrdinalIgnoreCase);
}
