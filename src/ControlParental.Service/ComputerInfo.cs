// <copyright file="ComputerInfo.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

/// <summary>
/// T24 — Result of gathering computer/device information.
/// </summary>
public sealed record DeviceInfoResult(
    string DeviceName,
    string DeviceModel,
    string OsVersion);

/// <summary>
/// T24 — Helper to gather device information via WMI.
/// </summary>
public static class ComputerInfo
{
    /// <summary>
    /// Gathers device information from the system.
    /// </summary>
    /// <returns>Device information result containing name, model, and OS version.</returns>
    public static DeviceInfoResult Gather()
    {
        string deviceModel;
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT Model FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                deviceModel = obj["Model"]?.ToString() ?? "Unknown";
                return new DeviceInfoResult(
                    DeviceName: Environment.MachineName,
                    DeviceModel: deviceModel,
                    OsVersion: Environment.OSVersion.ToString());
            }

            deviceModel = "Unknown";
        }
        catch
        {
            deviceModel = "Unknown";
        }

        return new DeviceInfoResult(
            DeviceName: Environment.MachineName,
            DeviceModel: deviceModel,
            OsVersion: Environment.OSVersion.ToString());
    }
}
