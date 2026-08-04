namespace ControlParental.Domain;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

/// <summary>Wire contract for the authenticated session pipe.</summary>
public static class IpcFrameCodec
{
    public const int HeaderSize = sizeof(int);
    public const int MaxFrameSize = 64 * 1024;

    public static byte[] Encode(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var payload = Encoding.UTF8.GetBytes(json);
        if (payload.Length > MaxFrameSize)
        {
            throw new InvalidDataException("IPC frame exceeds 64 KiB.");
        }

        var frame = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame, HeaderSize);
        return frame;
    }

    public sealed class Decoder
    {
        private readonly List<byte> pending = new();

        public IReadOnlyList<string> Append(ReadOnlySpan<byte> bytes)
        {
            this.pending.AddRange(bytes.ToArray());
            var frames = new List<string>();
            while (this.pending.Count >= HeaderSize)
            {
                var length = BinaryPrimitives.ReadInt32LittleEndian(CollectionsMarshal.AsSpan(this.pending));
                if (length <= 0 || length > MaxFrameSize)
                {
                    throw new InvalidDataException("IPC frame length is invalid.");
                }

                if (this.pending.Count < HeaderSize + length)
                {
                    break;
                }

                var json = Encoding.UTF8.GetString(this.pending.GetRange(HeaderSize, length).ToArray());
                try
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidDataException("IPC frame is not a JSON object.");
                    }
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException("IPC frame is malformed JSON.", ex);
                }

                frames.Add(json);
                this.pending.RemoveRange(0, HeaderSize + length);
            }

            return frames;
        }
    }
}
