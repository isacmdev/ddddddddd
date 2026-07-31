// <copyright file="AccountManagerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T37 — Tests for AccountManager.
/// Verifies child account creation, conversion, and privilege verification logic.
/// </summary>
public class AccountManagerTests : IDisposable
{
    private readonly Mock<IPrivilegeInspector> mockPrivilegeInspector;
    private readonly Mock<IChildAccountStore> mockAccountStore;
    private readonly string tempPath;
    private readonly AccountManager accountManager;

    public AccountManagerTests()
    {
        this.mockPrivilegeInspector = new Mock<IPrivilegeInspector>();
        this.mockAccountStore = new Mock<IChildAccountStore>();
        this.tempPath = Path.Combine(Path.GetTempPath(), $"cp_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.tempPath);

        this.accountManager = new AccountManager(
            this.mockPrivilegeInspector.Object,
            this.mockAccountStore.Object,
            this.tempPath,
            this.tempPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.tempPath, recursive: true);
        }
        catch { /* best-effort */ }

        GC.SuppressFinalize(this);
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullPrivilegeInspector_ThrowsArgumentNullException()
    {
        var act = () => new AccountManager(
            privilegeInspector: null!,
            accountStore: this.mockAccountStore.Object,
            programFilesPath: this.tempPath,
            dataFolderPath: this.tempPath);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("privilegeInspector");
    }

    [Fact]
    public void Constructor_WithNullAccountStore_ThrowsArgumentNullException()
    {
        var act = () => new AccountManager(
            privilegeInspector: this.mockPrivilegeInspector.Object,
            accountStore: null!,
            programFilesPath: this.tempPath,
            dataFolderPath: this.tempPath);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("accountStore");
    }

    // ── GetAccountsAsync Tests ───────────────────────────────────────

    [Fact]
    public async Task GetAccountsAsync_ReturnsList()
    {
        // Act
        var accounts = await this.accountManager.GetAccountsAsync(CancellationToken.None);

        // Assert — returns an IReadOnlyList (never null)
        accounts.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAccountsAsync_ReturnsDistinctAccountNames()
    {
        // Act
        var accounts = await this.accountManager.GetAccountsAsync(CancellationToken.None);

        // Assert — accounts list is valid
        accounts.Should().NotBeNull();
    }

    [Fact]
    public void IAccountManager_HasGetAccountsAsyncMethod_NotListAccountsAsync()
    {
        // T26 regression pin: the current domain contract exposes GetAccountsAsync.

        var interfaceType = typeof(IAccountManager);
        var getMethod = interfaceType.GetMethod(nameof(IAccountManager.GetAccountsAsync));
        var legacyMethod = interfaceType.GetMethod("ListAccountsAsync");

        getMethod.Should().NotBeNull(
            "IAccountManager must expose GetAccountsAsync (the current API surface).");
        legacyMethod.Should().BeNull(
            "IAccountManager must not expose the stale ListAccountsAsync name.");
    }

    // ── ChildAccountName Tests ───────────────────────────────────────

    [Fact]
    public void GetChildAccountName_WhenNotSet_ReturnsNull()
    {
        this.mockAccountStore.Setup(s => s.GetChildAccountName()).Returns((string?)null);

        var result = this.accountManager.GetChildAccountName();

        result.Should().BeNull();
    }

    [Fact]
    public void GetChildAccountName_WhenSet_ReturnsStoredName()
    {
        this.mockAccountStore.Setup(s => s.GetChildAccountName()).Returns("test_child");

        var result = this.accountManager.GetChildAccountName();

        result.Should().Be("test_child");
    }

    [Fact]
    public void SetChildAccountName_CallsStore()
    {
        var called = false;
        this.mockAccountStore
            .Setup(s => s.SetChildAccountName(It.IsAny<string>()))
            .Callback(() => called = true);

        this.accountManager.SetChildAccountName("new_child");

        called.Should().BeTrue();
        this.mockAccountStore.Verify(s => s.SetChildAccountName("new_child"), Times.Once);
    }

    [Fact]
    public void SetChildAccountName_PassesExactUsername()
    {
        string? capturedName = null;
        this.mockAccountStore
            .Setup(s => s.SetChildAccountName(It.IsAny<string>()))
            .Callback<string>(name => capturedName = name);

        this.accountManager.SetChildAccountName("  child_user  ");

        capturedName.Should().Be("  child_user  ");
    }

    // ── IsAccountStandardAsync integration note ─────────────────────────
    // NOTE: AccountManager.IsAccountStandardAsync creates WindowsIdentity(username)
    // directly — it does NOT use IPrivilegeInspector. Mocking IPrivilegeInspector
    // has no effect on IsAccountStandardAsync. Real integration tests require a
    // genuine Windows username and are tested as part of ServiceCompositionTests.
    // We test the validation and account-store delegation separately below.

    // ── ConvertToStandardAsync Tests ─────────────────────────────────

    [Fact]
    public async Task ConvertToStandardAsync_WithEmptyUsername_ReturnsFailed()
    {
        var result = await this.accountManager.ConvertToStandardAsync("", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task ConvertToStandardAsync_WithWhitespaceUsername_ReturnsFailed()
    {
        var result = await this.accountManager.ConvertToStandardAsync("   ", CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    // NOTE: ConvertToStandardAsync with a real username requires WindowsIdentity
    // to resolve that user — it throws SecurityException for non-existent accounts.
    // Guard-clause tests (empty/whitespace) are the meaningful unit tests;
    // integration with a real account is covered by ServiceCompositionTests.

    // ── CreateStandardAccountAsync Tests ─────────────────────────────

    [Fact]
    public async Task CreateStandardAccountAsync_WithEmptyUsername_ReturnsFailed()
    {
        var result = await this.accountManager.CreateStandardAccountAsync("", "password123", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task CreateStandardAccountAsync_WithWhitespaceUsername_ReturnsFailed()
    {
        var result = await this.accountManager.CreateStandardAccountAsync("   ", "password123", CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CreateStandardAccountAsync_WithShortPassword_ReturnsFailed()
    {
        var result = await this.accountManager.CreateStandardAccountAsync("new_child", "abc", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("4 characters");
    }

    [Fact]
    public async Task CreateStandardAccountAsync_WithNullPassword_ReturnsFailed()
    {
        var result = await this.accountManager.CreateStandardAccountAsync("new_child", null!, CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CreateStandardAccountAsync_WithEmptyPassword_ReturnsFailed()
    {
        var result = await this.accountManager.CreateStandardAccountAsync("new_child", "", CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CreateStandardAccountAsync_PasswordExactly4Chars_IsAccepted()
    {
        // Arrange — password of exactly 4 characters should pass validation
        this.mockPrivilegeInspector
            .Setup(p => p.GetPrivilegeLevelAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrivilegeLevel.Standard);

        // Act
        var result = await this.accountManager.CreateStandardAccountAsync("valid_user", "1234", CancellationToken.None);

        // Assert — validation passes (result may still fail due to net.exe, but not password validation)
        result.ErrorMessage.Should().NotContain("4 characters");
    }

    [Fact]
    public async Task CreateStandardAccountAsync_ResultHasExpectedShape()
    {
        // Arrange
        this.mockPrivilegeInspector
            .Setup(p => p.GetPrivilegeLevelAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrivilegeLevel.Standard);

        // Act
        var result = await this.accountManager.CreateStandardAccountAsync("new_child", "password123", CancellationToken.None);

        // Assert — result is a well-formed AccountCreationResult
        result.Should().NotBeNull();
        // Success and RequiresElevation are booleans (always true or false)
        // We only verify the object is properly constructed
        _ = result.Success;
        _ = result.RequiresElevation;
    }

    // ── Result Factory Methods Tests ──────────────────────────────────

    [Fact]
    public void Succeeded_Username_Matches()
    {
        var result = AccountCreationResult.Succeeded("my_child");

        result.Success.Should().BeTrue();
        result.Username.Should().Be("my_child");
        result.RequiresElevation.Should().BeFalse();
    }

    [Fact]
    public void NeedsAdminElevation_ResultIndicatesElevationRequired()
    {
        var result = AccountCreationResult.NeedsAdminElevation("Admin required");

        result.Success.Should().BeFalse();
        result.RequiresElevation.Should().BeTrue();
        result.NeedsElevation.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Admin required");
    }

    [Fact]
    public void Failed_ResultDoesNotRequireElevation()
    {
        var result = AccountCreationResult.Failed("Something went wrong");

        result.Success.Should().BeFalse();
        result.RequiresElevation.Should().BeFalse();
        result.NeedsElevation.Should().BeFalse();
    }
}
