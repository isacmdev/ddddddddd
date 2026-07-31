// <copyright file="DirectXDiagnostics.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

/// <summary>
/// T26 — Diagnostico de GPU y DirectX para capturar errores como D3D11CreateDevice() fallando.
/// Este modulo NO requiere DirectX para funcionar — solo reporta informacion del sistema.
/// </summary>
public static class DirectXDiagnostics
{
    private static readonly string LogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControlParental",
        "gpu_diagnostics.log");

    /// <inheritdoc/>
    public static void RunFullDiagnostics()
    {
        Log("[GPU-DIAG] ========== INICIO DIAGNOSTICO ==========");
        Log($"[GPU-DIAG] Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        Log($"[GPU-DIAG] OS: {Environment.OSVersion}");
        Log($"[GPU-DIAG] CLR: {Environment.Version}");
        Log($"[GPU-DIAG] Arch: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
        Log($"[GPU-DIAG] Machine: {Environment.MachineName}");
        Log($"[GPU-DIAG] User: {Environment.UserName}");

        EnumerateDisplayDrivers();
        EnumerateDirectXDlls();
        CheckWinAppRuntimeInstallations();
        CheckGraphicsEnvironmentVariables();
        CheckGPUPreferenceSettings();

        Log("[GPU-DIAG] ========== FIN DIAGNOSTICO ==========");
    }

    private static void EnumerateDisplayDrivers()
    {
        Log("[GPU-DIAG] --- Display Drivers ---");

        // Usar WMI para obtener informacion de GPUs
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_VideoController");

            int gpuIndex = 0;
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                gpuIndex++;
                Log($"[GPU-DIAG] === Adaptador de Video #{gpuIndex} ===");

                var name = obj["Name"]?.ToString() ?? "Unknown";
                var driverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown";
                var driverDate = obj["DriverDate"]?.ToString() ?? "Unknown";
                var status = obj["Status"]?.ToString() ?? "Unknown";
                var adapterRAM = obj["AdapterRAM"]?.ToString() ?? "Unknown";
                var adapterDACType = obj["AdapterDACType"]?.ToString() ?? "Unknown";
                var currentRefreshRate = obj["CurrentRefreshRate"]?.ToString() ?? "Unknown";
                var currentHorizontalResolution = obj["CurrentHorizontalResolution"]?.ToString() ?? "Unknown";
                var currentVerticalResolution = obj["CurrentVerticalResolution"]?.ToString() ?? "Unknown";
                var videoArchitecture = obj["VideoArchitecture"]?.ToString() ?? "Unknown";
                var videoMemoryType = obj["VideoMemoryType"]?.ToString() ?? "Unknown";

                Log($"[GPU-DIAG]   Name: {name}");
                Log($"[GPU-DIAG]   DriverVersion: {driverVersion}");
                Log($"[GPU-DIAG]   DriverDate: {driverDate}");
                Log($"[GPU-DIAG]   Status: {status}");
                Log($"[GPU-DIAG]   AdapterRAM: {adapterRAM}");
                Log($"[GPU-DIAG]   AdapterDACType: {adapterDACType}");
                Log($"[GPU-DIAG]   Resolution: {currentHorizontalResolution}x{currentVerticalResolution} @ {currentRefreshRate}Hz");
                Log($"[GPU-DIAG]   VideoArchitecture: {videoArchitecture}");
                Log($"[GPU-DIAG]   VideoMemoryType: {videoMemoryType}");

                // Informacion adicional de Direct3D via DXGI
                GetDxgiAdapterInfo(name);
            }

            if (gpuIndex == 0)
            {
                Log("[GPU-DIAG]   NO se encontraron adaptadores de video via WMI");
            }
        }
        catch (System.Management.ManagementException ex)
        {
            Log($"[GPU-DIAG]   WMI Error (permisos?): {ex.Message}");
            Log("[GPU-DIAG]   Sugerencia: Ejecutar como Administrador para acceso completo a WMI");
        }
        catch (Exception ex)
        {
            Log($"[GPU-DIAG]   WMI Error: {ex.Message}");
        }
    }

    private static void GetDxgiAdapterInfo(string gpuName)
    {
        // Intentar obtener info de DXGI
        // Esto NO crea un dispositivo D3D, solo consulta el adaptador
        try
        {
            // Intentar cargar dxgi.dll y obtener info del adaptador
            var dxgiPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "dxgi.dll");

            if (System.IO.File.Exists(dxgiPath))
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(dxgiPath);
                Log($"[GPU-DIAG]   dxgi.dll version: {versionInfo.FileVersion}");
            }

            // Informacion sobre la GPU basada en el nombre
            if (gpuName.Contains("AMD") || gpuName.Contains("Radeon"))
            {
                Log("[GPU-DIAG]   GPU Vendor: AMD");
                Log("[GPU-DIAG]   Nota: AMD RX 580 es Polaris (no RDNA2)");
                Log("[GPU-DIAG]   Nota: No soporta DirectX 12 Ultimate ray tracing");
            }
            else if (gpuName.Contains("NVIDIA") || gpuName.Contains("GeForce"))
            {
                Log("[GPU-DIAG]   GPU Vendor: NVIDIA");
            }
            else if (gpuName.Contains("Intel"))
            {
                Log("[GPU-DIAG]   GPU Vendor: Intel");
            }
        }
        catch (Exception ex)
        {
            Log($"[GPU-DIAG]   DXGI Info Error: {ex.Message}");
        }
    }

    private static void EnumerateDirectXDlls()
    {
        Log("[GPU-DIAG] --- DirectX DLLs ---");

        var dxDlls = new (string Name, string Description)[]
        {
            ("d3d11.dll", "Direct3D 11"),
            ("d3d12.dll", "Direct3D 12"),
            ("d3d12core.dll", "Direct3D 12 Core"),
            ("dxgi.dll", "DirectX Graphics Interface"),
            ("d3d10_1.dll", "Direct3D 10.1"),
            ("d3d10.dll", "Direct3D 10"),
            ("d3d9.dll", "Direct3D 9"),
            ("d3d8.dll", "Direct3D 8"),
            ("d3d7.dll", "Direct3D 7"),
            ("d3dim.dll", "Direct3D Immediate Mode"),
            ("d3dramp.dll", "Direct3D RAMP"),
            ("d3drm.dll", "Direct3D Retained Mode"),
        };

        var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);

        foreach (var (name, description) in dxDlls)
        {
            var dllPath = System.IO.Path.Combine(systemPath, name);
            if (System.IO.File.Exists(dllPath))
            {
                try
                {
                    var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(dllPath);
                    Log($"[GPU-DIAG]   {name,-20} ({description,-25}) = {version.FileVersion}");
                }
                catch
                {
                    Log($"[GPU-DIAG]   {name,-20} ({description,-25}) = existe (version ilegible)");
                }
            }
            else
            {
                Log($"[GPU-DIAG]   {name,-20} ({description,-25}) = NO ENCONTRADO");
            }
        }
    }

    private static void CheckWinAppRuntimeInstallations()
    {
        Log("[GPU-DIAG] --- Windows App Runtime ---");

        var searchPaths = new[]
        {
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WindowsAppRuntime"),
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "WindowsAppRuntime"),
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "WindowsAppRuntime"),
        };

        foreach (var basePath in searchPaths)
        {
            if (System.IO.Directory.Exists(basePath))
            {
                Log($"[GPU-DIAG]   Base: {basePath}");

                try
                {
                    // Buscar versiones instaladas
                    var installedVersions = System.IO.Directory.GetDirectories(basePath);
                    foreach (var versionPath in installedVersions)
                    {
                        var version = System.IO.Path.GetFileName(versionPath);
                        Log($"[GPU-DIAG]     Version: {version}");

                        // Listar DLLs principales en esta version
                        var dlls = new[] { "WindowsAppRuntime.dll", "Bootstrap.dll" };
                        foreach (var dll in dlls)
                        {
                            var dllPath = System.IO.Path.Combine(versionPath, dll);
                            if (System.IO.File.Exists(dllPath))
                            {
                                var verInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(dllPath);
                                Log($"[GPU-DIAG]       {dll} = {verInfo.FileVersion}");
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Log($"[GPU-DIAG]     Acceso denegado a: {basePath}");
                }
                catch (Exception ex)
                {
                    Log($"[GPU-DIAG]     Error: {ex.Message}");
                }
            }
        }

        // Verificar version de WinAppRuntime cargada actualmente
        try
        {
            var handle = NativeMethods.GetModuleHandle("Microsoft.WindowsAppRuntime.Bootstrap.dll");
            if (handle != IntPtr.Zero)
            {
                Log($"[GPU-DIAG]   Microsoft.WindowsAppRuntime.Bootstrap.dll CARGADO en memoria");
            }
        }
        catch
        {
            // No se pudo verificar
        }
    }

    private static void CheckGraphicsEnvironmentVariables()
    {
        Log("[GPU-DIAG] --- Graphics Environment Variables ---");

        var graphicsVars = new[]
        {
            "DISABLE_GPU_COMPOSITING",
            "WINUI_SUPPORTS_SOFTWARE_RENDERER",
            "__GL_SHADER_DISK_CACHE",
            "DXVK_STATE_CACHE",
            "DXVK_LOG_LEVEL",
            "DXVK_NUM_THREADS",
            "NVIDIA_SHADER_CACHE",
            "GPU_MAX_ALLOC",
            "ForceHWSch",
            "AdaptiveVSync",
        };

        foreach (var varName in graphicsVars)
        {
            try
            {
                var value = Environment.GetEnvironmentVariable(varName);
                if (!string.IsNullOrEmpty(value))
                {
                    Log($"[GPU-DIAG]   {varName} = {value}");
                }
            }
            catch
            {
                // Variable no existe o no se puede leer
            }
        }

        // Verificar si el proceso es de 64 bits
        Log($"[GPU-DIAG]   ProcessBitness: {(Environment.Is64BitProcess ? 64 : 32)}-bit");
    }

    private static void CheckGPUPreferenceSettings()
    {
        Log("[GPU-DIAG] --- GPU Preference Settings ---");

        try
        {
            // Leer la configuracion de GPU preference desde el registro
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\DirectX\UserGpuPreferences");
            if (key != null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    var value = key.GetValue(valueName);
                    Log($"[GPU-DIAG]   {valueName} = {value}");
                }
            }
            else
            {
                Log("[GPU-DIAG]   No hay configuracion UserGpuPreferences");
            }
        }
        catch (Exception ex)
        {
            Log($"[GPU-DIAG]   Error leyendo GPU Preference: {ex.Message}");
        }

        // Verificar configuracion de energia de GPU
        try
        {
            using var powerKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (powerKey != null)
            {
                var driverDesc = powerKey.GetValue("DriverDesc");
                if (driverDesc != null)
                {
                    Log($"[GPU-DIAG]   GPU DriverDesc: {driverDesc}");

                    // Buscar subkeys de instancias
                    foreach (var subKeyName in powerKey.GetSubKeyNames())
                    {
                        if (subKeyName.StartsWith('0'))
                        {
                            using var instanceKey = powerKey.OpenSubKey(subKeyName);
                            if (instanceKey != null)
                            {
                                var instDriverDesc = instanceKey.GetValue("DriverDesc");
                                if (instDriverDesc != null)
                                {
                                    Log($"[GPU-DIAG]   Instance {subKeyName}: {instDriverDesc}");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[GPU-DIAG]   Error leyendo GPU power settings: {ex.Message}");
        }
    }

    private static void Log(string message)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(LogPath)!;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(LogPath, $"{message}{Environment.NewLine}");
            System.Diagnostics.Debug.WriteLine(message);
        }
        catch
        {
            // Si falla el logging, no queremos crashear
        }
    }

    /// <summary>
    /// Intenta capturar el HRESULT de un fallo de D3D11CreateDevice.
    /// NO crea ningún dispositivo DirectX - solo reporta si la función está disponible.
    /// </summary>
    public static void VerifyD3D11CreateDeviceAvailability()
    {
        Log("[GPU-DIAG] --- D3D11CreateDevice Availability ---");

        try
        {
            var d3d11Path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "d3d11.dll");

            if (System.IO.File.Exists(d3d11Path))
            {
                var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(d3d11Path);
                Log($"[GPU-DIAG]   d3d11.dll version: {version.FileVersion}");
                Log($"[GPU-DIAG]   D3D11CreateDevice ESTÁ disponible");
                Log("[GPU-DIAG]   NOTA: El error D3D11CreateDevice() ocurre внутри WinUI3,");
                Log("[GPU-DIAG]   no en nuestro codigo. El error viene del compositor interno.");
            }
            else
            {
                Log("[GPU-DIAG]   d3d11.dll NO ENCONTRADO - DirectX 11 no instalado!");
                Log("[GPU-DIAG]   Esto causaría D3D11CreateDevice() fallando.");
            }
        }
        catch (Exception ex)
        {
            Log($"[GPU-DIAG]   Error verificando D3D11CreateDevice: {ex.Message}");
        }
    }

    /// <summary>
    /// Genera un reporte resumido para el usuario.
    /// </summary>
    /// <returns></returns>
    public static string GenerateSummaryReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== REPORTE DE DIAGNOSTICO GRAFICO ===");
        report.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        report.AppendLine("Archivos de log:");
        report.AppendLine($"  - Startup: {System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControlParental", "app_startup.log")}");
        report.AppendLine($"  - GPU: {LogPath}");
        report.AppendLine();
        report.AppendLine("Para compartir este diagnostico:");
        report.AppendLine("  1. Abre la carpeta %LOCALAPPDATA%\\ControlParental");
        report.AppendLine("  2. Busca los archivos .log");
        report.AppendLine("  3. Compartelos con el desarrollador");
        report.AppendLine();
        report.AppendLine("Si el error persiste:");
        report.AppendLine("  1. Ejecuta 'dxdiag /whql:off /saveall %TEMP%\\dxdiag.txt'");
        report.AppendLine("  2. Guarda el archivo dxdiag.txt");
        report.AppendLine("  3. Revisa si hay errores en la pestana DirectX Files");

        return report.ToString();
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
