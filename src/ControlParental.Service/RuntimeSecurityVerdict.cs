// <copyright file="RuntimeSecurityVerdict.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// The service-owned security result used by health and onboarding.
/// </summary>
public enum RuntimeSecurityVerdict
{
    HealthyStandard,
    Administrator,
    Unknown,
    AclFailure,
}

/// <summary>
/// Contains the only interpretation of privilege and ACL outcomes.
/// </summary>
public static class RuntimeSecurityVerdictEvaluator
{
    public static RuntimeSecurityVerdict Evaluate(PrivilegeLevel privilegeLevel, bool aclSucceeded)
    {
        if (!aclSucceeded)
        {
            return RuntimeSecurityVerdict.AclFailure;
        }

        return privilegeLevel switch
        {
            PrivilegeLevel.Standard => RuntimeSecurityVerdict.HealthyStandard,
            PrivilegeLevel.Administrator => RuntimeSecurityVerdict.Administrator,
            _ => RuntimeSecurityVerdict.Unknown,
        };
    }

    public static bool IsHealthy(RuntimeSecurityVerdict verdict) =>
        verdict is RuntimeSecurityVerdict.HealthyStandard or RuntimeSecurityVerdict.Administrator;
}
