namespace ControlParental.Service.Interop;

using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography.X509Certificates;

internal static class AuthenticodeSigner
{
    public static string? GetSigner(int processId)
    {
        try
        {
            using var identity = Open(processId);
            return GetSigner(identity, out _);
        }
        catch
        {
            return null;
        }
    }

    internal static ProcessIdentity Open(int processId) => ProcessIdentity.Open(processId);

    internal static string? GetSigner(ProcessIdentity identity, out int rawResult)
    {
        using var trust = new WinTrustFileInfo(identity.ImagePath, WinTrust.GenericVerifyV2ActionId);
        rawResult = trust.RawResult;
        try
        {
            return GetCertificateThumbprint(identity.ImagePath);
        }
        catch
        {
            return null;
        }
    }

    public static string? GetSigner(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            return GetCertificateThumbprint(path);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCertificateThumbprint(string path)
    {
        using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
        return certificate.Thumbprint;
    }
}

internal sealed class ProcessIdentity : IDisposable
{
    private ProcessIdentity(SafeProcessHandle handle, string imagePath, int sessionId, DateTime startTimeUtc)
    {
        this.Handle = handle;
        this.ImagePath = imagePath;
        this.SessionId = sessionId;
        this.StartTimeUtc = startTimeUtc;
    }

    public SafeProcessHandle Handle { get; }
    public string ImagePath { get; }
    public int SessionId { get; }
    public DateTime StartTimeUtc { get; }

    public static ProcessIdentity Open(int processId)
    {
        var handle = OpenProcess(0x1000, false, (uint)processId);
        if (handle == IntPtr.Zero) throw new InvalidOperationException("Process handle unavailable.");
        var safeHandle = new SafeProcessHandle(handle, ownsHandle: true);
        try
        {
            var capacity = 4096;
            var path = new StringBuilder(capacity);
            if (!QueryFullProcessImageName(safeHandle, 0, path, ref capacity)) throw new InvalidOperationException("Process path unavailable.");
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return new ProcessIdentity(safeHandle, path.ToString(), process.SessionId, process.StartTime.ToUniversalTime());
        }
        catch
        {
            safeHandle.Dispose();
            throw;
        }
    }

    public void Dispose() => this.Handle.Dispose();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder imagePath, ref int size);
}
