namespace ControlParental.Domain.Tests;

using System.Buffers.Binary;
using ControlParental.Domain;
using Xunit;

public sealed class IpcFrameCodecTests
{
    [Fact]
    public void Decoder_ReassemblesFragmentedAndCoalescedFrames()
    {
        var first = IpcFrameCodec.Encode("{\"MessageType\":\"Ping\"}");
        var second = IpcFrameCodec.Encode("{\"MessageType\":\"Pong\"}");
        var decoder = new IpcFrameCodec.Decoder();

        Assert.Empty(decoder.Append(first[..2]));
        Assert.Equal("{\"MessageType\":\"Ping\"}", decoder.Append(first[2..])[0]);
        Assert.Equal(new[] { "{\"MessageType\":\"Pong\"}" }, decoder.Append(second));
    }

    [Fact]
    public void Decoder_RejectsMalformedAndOversizedFrames()
    {
        Assert.Throws<InvalidDataException>(
            () => new IpcFrameCodec.Decoder().Append(IpcFrameCodec.Encode("not-json")));

        var oversized = new byte[IpcFrameCodec.MaxFrameSize + IpcFrameCodec.HeaderSize];
        BinaryPrimitives.WriteInt32LittleEndian(oversized, IpcFrameCodec.MaxFrameSize + 1);

        Assert.Throws<InvalidDataException>(
            () => new IpcFrameCodec.Decoder().Append(oversized));
    }
}
