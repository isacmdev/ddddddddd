// <copyright file="ConfigurationLoaderTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Security;
using ControlParental.Service;
using FluentAssertions;
using Xunit;

public sealed class ConfigurationLoaderTests
{
    [Fact]
    public void ParseFile_ValidHttpsConfiguration_ReturnsValuesWithoutLoggingSecrets()
    {
        var result = ConfigurationLoader.ParseFile(
            "SUPABASE_URL=https://example.supabase.co\nSUPABASE_ANON_KEY=sb_publishable_test-key\nSUPABASE_CERT_PINS=sha256/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\n");

        result.IsValid.Should().BeTrue();
        result.Config.Should().Be(new SupabaseConfig("https://example.supabase.co", "sb_publishable_test-key", "sha256/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="));
        result.Error.Should().BeNull();
        result.Diagnostic.Should().NotContain("sb_publishable_test-key");
    }

    [Theory]
    [InlineData("SUPABASE_URL=http://example.supabase.co\nSUPABASE_ANON_KEY=key")]
    [InlineData("SUPABASE_URL=https://example.supabase.co\nSUPABASE_ANON_KEY=service_role_secret")]
    [InlineData("SUPABASE_URL=https://example.supabase.co\nSUPABASE_ANON_KEY=one-character")]
    [InlineData("SUPABASE_URL=https://example.supabase.co\n")]
    public void ParseFile_InvalidConfiguration_FailsClosed(string contents)
    {
        var result = ConfigurationLoader.ParseFile(contents);

        result.IsValid.Should().BeFalse();
        result.Config.Should().BeNull();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.Diagnostic.Should().NotContain("service_role_secret");
    }

    [Fact]
    public void ParseFile_DuplicateKeys_FailsClosed()
    {
        var result = ConfigurationLoader.ParseFile(
            "SUPABASE_URL=https://one.supabase.co\nSUPABASE_URL=https://two.supabase.co\nSUPABASE_ANON_KEY=key");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("duplicate");
    }

    [Fact]
    public void ParseFile_RejectsEncodedServiceRoleJwt()
    {
        var result = ConfigurationLoader.ParseFile(
            "SUPABASE_URL=https://example.supabase.co\nSUPABASE_ANON_KEY=eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoic2VydmljZV9yb2xlIn0.signature");

        result.IsValid.Should().BeFalse();
        result.Config.Should().BeNull();
        result.Diagnostic.Should().NotContain("service_role");
    }

    [Fact]
    public void ParseFile_RejectsJwtWithNonAnonRole()
    {
        var result = ConfigurationLoader.ParseFile(
            "SUPABASE_URL=https://example.supabase.co\nSUPABASE_ANON_KEY=eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoidXNlciJ9.signature");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ParseFile_JwtWithNonStringRole_FailsClosedWithoutThrowing()
    {
        var result = ConfigurationLoader.ParseFile(
            "SUPABASE_URL=https://example.supabase.co\nSUPABASE_ANON_KEY=eyJhbGciOiJub25lIn0.eyJyb2xlIjoxMjN9.signature");

        result.IsValid.Should().BeFalse();
        result.Config.Should().BeNull();
    }
}
