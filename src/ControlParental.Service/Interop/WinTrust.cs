// <copyright file="WinTrust.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Interop;

using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// P/Invoke wrappers for WinVerifyTrust API.
/// Used to verify Authenticode signatures of the service binary.
/// </summary>
public static class WinTrust
{
    public const uint WINTRUST_ACTION_GENERIC_VERIFY_V2 = 0x00AAC60B;

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
    public static extern int WinVerifyTrust(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID,
        ref WINTRUST_DATA pWVTData);

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
    public static extern int WinVerifyTrust(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID,
        IntPtr pWVTData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateData;
        public IntPtr pszStateData;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    public const uint WTD_UI_NONE = 2;
    public const uint WTD_REVOKE_NONE = 0;
    public const uint WTD_CHOICE_FILE = 1;
}

/// <summary>
/// Wrapper for WinVerifyTrust to verify Authenticode signatures.
/// </summary>
public sealed class WinTrustFileInfo : IDisposable
{
    private readonly string filePath;
    private bool isSigned;
    private bool disposed;

    public WinTrustFileInfo(string filePath, Guid actionId)
    {
        this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

        try
        {
            var fileInfo = new WinTrust.WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WinTrust.WINTRUST_FILE_INFO>(),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero,
            };

            var fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrust.WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, fDeleteOld: false);

            var trustData = new WinTrust.WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WinTrust.WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WinTrust.WTD_UI_NONE,
                fdwRevocationChecks = WinTrust.WTD_REVOKE_NONE,
                dwUnionChoice = WinTrust.WTD_CHOICE_FILE,
                pFile = fileInfoPtr,
                dwStateData = 0,
                pszStateData = IntPtr.Zero,
                dwUIContext = 0,
                pSignatureSettings = IntPtr.Zero,
            };

            var result = WinTrust.WinVerifyTrust(
                IntPtr.Zero,
                actionId,
                ref trustData);

            this.isSigned = result == 0;

            Marshal.FreeHGlobal(fileInfoPtr);
        }
        catch
        {
            this.isSigned = false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the file has a valid Authenticode signature.
    /// </summary>
    public bool IsSigned => this.isSigned;

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
        }
    }
}
