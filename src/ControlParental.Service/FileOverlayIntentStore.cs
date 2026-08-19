namespace ControlParental.Service;

using System.Text.Json;
using ControlParental.Domain;

public sealed class FileOverlayIntentStore : IOverlayIntentStore
{
    private readonly string path;

    public FileOverlayIntentStore(string path)
    {
        this.path = string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("An overlay intent path is required.", nameof(path))
            : path;
    }

    public OverlayIntent? Load()
    {
        if (!File.Exists(this.path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(this.path));
            return document is { Version: 1, Intent: not null }
                ? document.Intent
                : throw new InvalidDataException("The overlay intent document is incomplete.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The overlay intent document is corrupt.", exception);
        }
    }

    public void Save(OverlayIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var directory = Path.GetDirectoryName(this.path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{this.path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Document(1, intent)));
            File.Move(temporary, this.path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private sealed record Document(int Version, OverlayIntent Intent);
}
