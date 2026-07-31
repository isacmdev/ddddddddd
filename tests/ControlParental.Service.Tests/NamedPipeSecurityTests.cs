// <copyright file="NamedPipeSecurityTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using ControlParental.Service.Interop;
using Xunit;

/// <summary>
/// T37/T38 — Regression tests for named pipe ACL construction.
/// These tests verify that agent and UI pipes are locked down to the expected principals.
/// </summary>
public sealed class NamedPipeSecurityTests
{
    [Fact]
    public void CreatePipeSecurity_AgentPipe_AllowsLocalSystemAndChildOnly()
    {
        // Arrange
        var childSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        // Act
        var security = NamedPipeServer.CreatePipeSecurity(childSid);

        // Assert
        Assert.NotNull(security);
        Assert.True(security.AreAccessRulesProtected, "Agent pipe ACL must be protected from inheritance.");

        var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        Assert.Contains(rules, r =>
            r.IdentityReference.Value == localSystem.Value &&
            r.PipeAccessRights.HasFlag(PipeAccessRights.FullControl) &&
            r.AccessControlType == AccessControlType.Allow);

        Assert.Contains(rules, r =>
            r.IdentityReference.Value == childSid.Value &&
            r.PipeAccessRights.HasFlag(PipeAccessRights.ReadWrite) &&
            r.AccessControlType == AccessControlType.Allow);

        // The child must not receive CreateNewInstance, which would let it spawn a rogue server.
        Assert.DoesNotContain(rules, r =>
            r.IdentityReference.Value == childSid.Value &&
            r.AccessControlType == AccessControlType.Allow &&
            r.PipeAccessRights.HasFlag(PipeAccessRights.CreateNewInstance));
    }

    [Fact]
    public void CreatePipeSecurity_UIPipe_DefaultRestrictsToLocalSystemAndAdministrators()
    {
        // Act
        var security = NamedPipeUIServer.CreatePipeSecurity();

        // Assert
        Assert.NotNull(security);
        Assert.True(security.AreAccessRulesProtected, "UI pipe ACL must be protected from inheritance.");

        var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var guests = new SecurityIdentifier(WellKnownSidType.BuiltinGuestsSid, null);

        Assert.Contains(rules, r =>
            r.IdentityReference.Value == localSystem.Value &&
            r.PipeAccessRights.HasFlag(PipeAccessRights.FullControl) &&
            r.AccessControlType == AccessControlType.Allow);

        Assert.Contains(rules, r =>
            r.IdentityReference.Value == admins.Value &&
            r.PipeAccessRights.HasFlag(PipeAccessRights.ReadWrite) &&
            r.AccessControlType == AccessControlType.Allow);

        Assert.Contains(rules, r =>
            r.IdentityReference.Value == guests.Value &&
            r.AccessControlType == AccessControlType.Deny);

        // The old "any interactive user" rule must not exist.
        var interactive = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);
        Assert.DoesNotContain(rules, r => r.IdentityReference.Value == interactive.Value);
    }

    [Fact]
    public void CreatePipeSecurity_UIPipe_WithParentSid_AllowsParentAndDeniesChild()
    {
        // Arrange
        var parentSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var childSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        // Act
        var security = NamedPipeUIServer.CreatePipeSecurity(parentSid, childSid);

        // Assert
        var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        Assert.Contains(rules, r =>
            r.IdentityReference.Value == parentSid.Value &&
            r.PipeAccessRights.HasFlag(PipeAccessRights.ReadWrite) &&
            r.AccessControlType == AccessControlType.Allow);

        Assert.Contains(rules, r =>
            r.IdentityReference.Value == childSid.Value &&
            r.AccessControlType == AccessControlType.Deny);

        var guests = new SecurityIdentifier(WellKnownSidType.BuiltinGuestsSid, null);
        Assert.Contains(rules, r =>
            r.IdentityReference.Value == guests.Value &&
            r.AccessControlType == AccessControlType.Deny);
    }

    [Fact]
    public void ValidateClientSid_RejectsNullAndPredicateMismatch()
    {
        // Arrange
        var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        // Act + Assert
        Assert.False(NamedPipeServer.ValidateClientSid(null, _ => true));
        Assert.False(NamedPipeServer.ValidateClientSid(sid, _ => false));
    }

    [Fact]
    public void ValidateClientSid_AllowsAcceptedSid()
    {
        // Arrange
        var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        // Act
        var allowed = NamedPipeServer.ValidateClientSid(sid, value => value == sid.Value);

        // Assert
        Assert.True(allowed);
    }
}
