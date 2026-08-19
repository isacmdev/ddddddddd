namespace ControlParental.Domain.Tests;

using ControlParental.Domain;
using Xunit;

public sealed class IpcHandshakeTests
{
    [Fact]
    public void RoundTrip_BindsSessionPidAndSigner()
    {
        var json = IpcHandshake.Create(7, 1234, "trusted");

        Assert.True(IpcHandshake.TryParse(json, out var handshake));
        Assert.True(IpcHandshake.IsAuthorized(handshake, 7, 1234, "TRUSTED"));
        Assert.False(IpcHandshake.IsAuthorized(handshake, 8, 1234, "trusted"));
        Assert.False(IpcHandshake.IsAuthorized(handshake, 7, 9999, "trusted"));
        Assert.False(IpcHandshake.IsAuthorized(handshake, 7, 1234, "other"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("raw legacy payload")]
    [InlineData("{\"MessageType\":\"IpcHandshake\",\"SessionId\":1}")]
    public void Parse_RejectsRawOrIncompleteHandshake(string json)
    {
        Assert.False(IpcHandshake.TryParse(json, out _));
    }

    [Theory]
    [InlineData("{\"MessageType\":\"Other\",\"SessionId\":1,\"ProcessId\":2,\"Signer\":\"trusted\"}")]
    [InlineData("{\"MessageType\":\"IpcHandshake\",\"SessionId\":-1,\"ProcessId\":2,\"Signer\":\"trusted\"}")]
    [InlineData("{\"MessageType\":\"IpcHandshake\",\"SessionId\":1,\"ProcessId\":0,\"Signer\":\"trusted\"}")]
    [InlineData("{\"MessageType\":\"IpcHandshake\",\"SessionId\":1,\"ProcessId\":2,\"Signer\":\"\"}")]
    public void Parse_RejectsWrongTypeInvalidIdentityOrEmptySigner(string json)
    {
        Assert.False(IpcHandshake.TryParse(json, out _));
    }

    [Theory]
    [InlineData("{\"MessageType\":123,\"SessionId\":1,\"ProcessId\":2,\"Signer\":\"trusted\"}")]
    [InlineData("{\"MessageType\":\"IpcHandshake\",\"SessionId\":1,\"ProcessId\":2,\"Signer\":123}")]
    public void Parse_RejectsWrongJsonValueTypes(string json)
    {
        Assert.False(IpcHandshake.TryParse(json, out _));
    }

    [Fact]
    public void IsAuthorized_RejectsBlankExpectedSigner()
    {
        var handshake = new IpcHandshake(7, 1234, "trusted");

        Assert.False(IpcHandshake.IsAuthorized(handshake, 7, 1234, string.Empty));
        Assert.False(IpcHandshake.IsAuthorized(handshake, 7, 1234, "  "));
    }
}
