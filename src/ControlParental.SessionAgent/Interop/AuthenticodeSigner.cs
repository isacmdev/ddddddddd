namespace ControlParental.SessionAgent.Interop;

using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography.X509Certificates;

internal static class AuthenticodeSigner
{
    internal static readonly Guid GenericVerifyV2ActionId = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

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
        using var trust = new WinTrustFileInfo(identity.ImagePath);
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
    { Handle = handle; ImagePath = imagePath; SessionId = sessionId; StartTimeUtc = startTimeUtc; }
    public SafeProcessHandle Handle { get; }
    public string ImagePath { get; }
    public int SessionId { get; }
    public DateTime StartTimeUtc { get; }
    public static ProcessIdentity Open(int processId)
    {
        var raw = OpenProcess(0x1000, false, (uint)processId);
        if (raw == IntPtr.Zero) throw new InvalidOperationException("Process handle unavailable.");
        var handle = new SafeProcessHandle(raw, true);
        try
        {
            var length = 4096;
            var path = new StringBuilder(length);
            if (!QueryFullProcessImageName(handle, 0, path, ref length)) throw new InvalidOperationException("Process path unavailable.");
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return new ProcessIdentity(handle, path.ToString(), process.SessionId, process.StartTime.ToUniversalTime());
        }
        catch { handle.Dispose(); throw; }
    }
    public void Dispose() => Handle.Dispose();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int length);
}

internal sealed class WinTrustFileInfo : IDisposable
{
    private readonly IntPtr fileInfo;
    public WinTrustFileInfo(string path)
    {
        var info = new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(), pcwszFilePath = path };
        this.fileInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
        Marshal.StructureToPtr(info, this.fileInfo, false);
        var data = new WINTRUST_DATA
        {
            cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
            dwUnionChoice = 1,
            pFile = this.fileInfo,
            dwUIChoice = 2,
            dwProvFlags = 0,
        };
        RawResult = WinVerifyTrust(IntPtr.Zero, AuthenticodeSigner.GenericVerifyV2ActionId, ref data);
    }
    public int RawResult { get; }
    public void Dispose() => Marshal.FreeHGlobal(fileInfo);
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern int WinVerifyTrust(IntPtr hwnd, Guid action, ref WINTRUST_DATA data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }
}
