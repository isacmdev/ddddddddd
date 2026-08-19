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
    public static readonly Guid GenericVerifyV2ActionId = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

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
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
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

    public int RawResult { get; private set; } = int.MinValue;

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
                dwStateAction = 0,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = 0,
                dwUIContext = 0,
                pSignatureSettings = IntPtr.Zero,
            };

            this.RawResult = WinTrust.WinVerifyTrust(
                IntPtr.Zero,
                actionId,
                ref trustData);

            this.isSigned = this.RawResult == 0;
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
