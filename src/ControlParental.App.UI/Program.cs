// <copyright file="Program.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;

    /// <summary>
    /// WinUI 3 application entry point.
    /// DISABLE_XAML_GENERATED_MAIN is set in the csproj so this Program.Main()
    /// is used instead of the auto-generated one from App.g.i.cs.
    /// </summary>
    internal sealed class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [STAThread]
        private static void Main()
        {
            // CRITICAL: SetDllDirectory to local exe directory so local WinUI DLLs
            // take priority over incompatible system DLLs (e.g. Microsoft.UI.Xaml.dll
            // v3.1.8.0 installed by other apps/VS). Must be called BEFORE any WinRT
            // interop to prevent 0xc000027b on GPUs like AMD RX 580.
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(exeDir))
            {
                SetDllDirectory(exeDir);
            }

            // CRITICAL: Register code pages encoding provider for ANSI support
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // CRITICAL: Initialize WinRT COM wrappers before ANY WinRT interop
            // This fixes 0x80040154 (REGDB_E_CLASSNOTREG) on machines where
            // WinRT classes are not registered globally.
            WinRT.ComWrappersSupport.InitializeComWrappers();

            // Start the application with proper DispatcherQueue synchronization context
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);

                _ = new App();
            });
        }
    }
}
