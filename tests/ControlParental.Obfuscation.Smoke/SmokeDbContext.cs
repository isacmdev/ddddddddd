// <copyright file="SmokeDbContext.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Obfuscation.Smoke;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// T23 Obfuscation Closure — smoke-only DbContext that mirrors the
/// <c>ControlParentalDbContext</c> model configuration for
/// <c>PolicyDbEntity</c>, but operates on the obfuscated Type supplied
/// at runtime (rather than the unobfuscated reference assembly Type).
///
/// The smoke project deliberately does not reference the Domain
/// assembly at compile time: the <c>PolicyDbEntity</c> Type is
/// resolved from the obfuscated Domain assembly loaded at runtime.
/// Property / column names are therefore expressed as string
/// constants so the smoke can compile without the Domain reference.
/// </summary>
internal sealed class SmokeDbContext : DbContext
{
    // Hardcoded property + column names (must stay in sync with
    // PolicyDbEntity.cs in the Domain project and
    // ControlParentalDbContext.OnModelCreating in Service). Strings
    // are used so the smoke can compile without referencing Domain.
    private const string DeviceIdProperty = "DeviceId";
    private const string VersionProperty = "Version";
    private const string PolicyJsonProperty = "PolicyJson";
    private const string LastUpdatedProperty = "LastUpdated";
    private const string CategoryAssignmentsJsonProperty = "CategoryAssignmentsJson";

    private readonly Type policyEntityType;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmokeDbContext"/> class.
    /// </summary>
    /// <param name="options">The EF Core context options (SQLite in-memory).</param>
    /// <param name="policyEntityType">
    /// The <see cref="Type"/> of <c>PolicyDbEntity</c> resolved from the
    /// obfuscated Domain assembly. Must be the obfuscated type — not
    /// the unobfuscated reference Type — for this smoke to actually
    /// exercise the obfuscated artifact.
    /// </param>
    public SmokeDbContext(DbContextOptions<SmokeDbContext> options, Type policyEntityType)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(policyEntityType);
        this.policyEntityType = policyEntityType;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Mirror ControlParentalDbContext.OnModelCreating for PolicyDbEntity.
        // The column names are snake_case to match the production
        // schema; the property names MUST match the public property
        // names of the obfuscated PolicyDbEntity. KeepPublicApi=true
        // guarantees the public names are preserved.
        var entity = modelBuilder.Entity(this.policyEntityType);
        entity.ToTable("policies");
        entity.HasKey(DeviceIdProperty);

        entity.Property(typeof(string), DeviceIdProperty)
            .HasColumnName("device_id");
        entity.Property(typeof(int), VersionProperty)
            .HasColumnName("version");
        entity.Property(typeof(string), PolicyJsonProperty)
            .HasColumnName("policy_json");
        entity.Property(typeof(DateTimeOffset), LastUpdatedProperty)
            .HasColumnName("last_updated");
        entity.Property(typeof(string), CategoryAssignmentsJsonProperty)
            .HasColumnName("category_assignments_json");

        entity.HasIndex(VersionProperty);
    }
}
