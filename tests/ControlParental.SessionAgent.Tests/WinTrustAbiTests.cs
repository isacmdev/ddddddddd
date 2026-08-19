namespace ControlParental.SessionAgent.Tests;

using System.Runtime.InteropServices;
using ControlParental.Domain;
using ControlParental.SessionAgent.Interop;
using Xunit;

public sealed class WinTrustAbiTests
{
    [Fact]
    public void GenericVerifyV2Action_IsOfficial()
    {
        Assert.Equal(
            new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE"),
            AuthenticodeSigner.GenericVerifyV2ActionId);
    }

    [Fact]
    public void WinTrustData_MatchesTheNativePointerSizedLayout()
    {
        Assert.Equal(IntPtr.Size == 8 ? 88 : 52, Marshal.SizeOf<WinTrustFileInfo.WINTRUST_DATA>());
        Assert.Equal(IntPtr.Size == 8 ? 24 : 12, Marshal.OffsetOf<WinTrustFileInfo.WINTRUST_DATA>(nameof(WinTrustFileInfo.WINTRUST_DATA.dwUIChoice)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 40 : 24, Marshal.OffsetOf<WinTrustFileInfo.WINTRUST_DATA>(nameof(WinTrustFileInfo.WINTRUST_DATA.pFile)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 48 : 28, Marshal.OffsetOf<WinTrustFileInfo.WINTRUST_DATA>(nameof(WinTrustFileInfo.WINTRUST_DATA.dwStateAction)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 64 : 36, Marshal.OffsetOf<WinTrustFileInfo.WINTRUST_DATA>(nameof(WinTrustFileInfo.WINTRUST_DATA.pwszURLReference)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 72 : 40, Marshal.OffsetOf<WinTrustFileInfo.WINTRUST_DATA>(nameof(WinTrustFileInfo.WINTRUST_DATA.dwProvFlags)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 76 : 44, Marshal.OffsetOf<WinTrustFileInfo.WINTRUST_DATA>(nameof(WinTrustFileInfo.WINTRUST_DATA.dwUIContext)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 80 : 48, Marshal.OffsetOf<WinTrustFileInfo.WINTRUST_DATA>(nameof(WinTrustFileInfo.WINTRUST_DATA.pSignatureSettings)).ToInt32());
    }

    [Fact]
    public void WinTrustData_ProviderFlagsDefaultToZero()
    {
        var data = new WinTrustFileInfo.WINTRUST_DATA();

        Assert.Equal(0u, data.dwProvFlags);
        Assert.Equal(IntPtr.Zero, data.hWVTStateData);
        Assert.Equal(IntPtr.Zero, data.pwszURLReference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ProcessIdentityLookup_RejectsUnavailableProcess(int processId)
    {
        Assert.Null(AuthenticodeSigner.GetSigner(processId));
    }

    [Fact]
    public void ProcessIdentity_ExposesManagedIdentityAndRawTrustResult()
    {
        using var identity = AuthenticodeSigner.Open(Environment.ProcessId);
        var signer = AuthenticodeSigner.GetSigner(identity, out var rawResult);

        Assert.False(identity.Handle.IsInvalid);
        Assert.False(identity.Handle.IsClosed);
        Assert.True(identity.SessionId >= 0);
        Assert.True(identity.StartTimeUtc <= DateTime.UtcNow);
        Assert.Equal(signer, AuthenticodeSigner.GetSigner(Environment.ProcessId));
        Assert.NotEqual(int.MinValue, rawResult);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-signed-file")]
    public void SignerLookup_RejectsMissingOrUnsignedPath(string? path)
    {
        Assert.Null(AuthenticodeSigner.GetSigner(path));
    }

    [Fact]
    public async Task ClientSendAsync_WithoutConnection_DoesNotWriteRawFallback()
    {
        using var client = new NamedPipeClient("unconnected");

        Assert.False(client.IsConnected);
        await client.SendAsync(new Pong());
        Assert.False(client.IsConnected);
    }
}
