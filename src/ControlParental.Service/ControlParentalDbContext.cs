// <copyright file="ControlParentalDbContext.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Text.Json;
using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// T03 — SQLite DbContext for local persistence.
/// Source of truth offline; all writes go here first.
/// </summary>
public sealed class ControlParentalDbContext : DbContext
{
    public DbSet<PolicyDbEntity> Policies => this.Set<PolicyDbEntity>();
    public DbSet<AppPolicyDbEntity> AppPolicies => this.Set<AppPolicyDbEntity>();
    public DbSet<GrantDbEntity> Grants => this.Set<GrantDbEntity>();
    public DbSet<UsageTodayDbEntity> UsageToday => this.Set<UsageTodayDbEntity>();
    public DbSet<OutboxDbEntity> Outbox => this.Set<OutboxDbEntity>();
    public DbSet<ForegroundEventDbEntity> ForegroundEvents => this.Set<ForegroundEventDbEntity>();
    public DbSet<ReconciliationHistoryDbEntity> ReconciliationHistory => this.Set<ReconciliationHistoryDbEntity>();
    public DbSet<ConsentDbEntity> Consent => this.Set<ConsentDbEntity>();

    public ControlParentalDbContext(DbContextOptions<ControlParentalDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Policy ───────────────────────────────────────────────────────
        modelBuilder.Entity<PolicyDbEntity>(entity =>
        {
            entity.ToTable("policies");
            entity.HasKey(e => e.DeviceId);
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.PolicyJson).HasColumnName("policy_json");
            entity.Property(e => e.LastUpdated).HasColumnName("last_updated");
            entity.Property(e => e.CategoryAssignmentsJson).HasColumnName("category_assignments_json");
            entity.HasIndex(e => e.Version);
        });

        // ── AppPolicy ─────────────────────────────────────────────────────
        modelBuilder.Entity<AppPolicyDbEntity>(entity =>
        {
            entity.ToTable("app_policies");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.PackageName).HasColumnName("package_name");
            entity.Property(e => e.State).HasColumnName("state");
            entity.Property(e => e.DailyLimitMinutes).HasColumnName("daily_limit_minutes");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.AllowedWindowsJson).HasColumnName("allowed_windows_json");
            entity.HasIndex(e => new { e.DeviceId, e.PackageName }).IsUnique();
            entity.HasOne(e => e.Policy)
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Grant ─────────────────────────────────────────────────────────
        modelBuilder.Entity<GrantDbEntity>(entity =>
        {
            entity.ToTable("grants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.GrantId).HasColumnName("grant_id");
            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.Scope).HasColumnName("scope");
            entity.Property(e => e.Minutes).HasColumnName("minutes");
            entity.Property(e => e.GrantedAt).HasColumnName("granted_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.Source).HasColumnName("source");
            entity.HasIndex(e => new { e.DeviceId, e.GrantId }).IsUnique();
            entity.HasIndex(e => e.ExpiresAt); // For cleanup of expired grants
            entity.HasOne(e => e.Policy)
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── UsageToday ────────────────────────────────────────────────────
        modelBuilder.Entity<UsageTodayDbEntity>(entity =>
        {
            entity.ToTable("usage_today");
            entity.HasKey(e => new { e.AppId, e.ServerDate });
            entity.Property(e => e.AppId).HasColumnName("app_id");
            entity.Property(e => e.ServerDate).HasColumnName("server_date");
            entity.Property(e => e.Minutes).HasColumnName("minutes");
            entity.Property(e => e.LastUpdated).HasColumnName("last_updated");
        });

        // ── Outbox ───────────────────────────────────────────────────────
        modelBuilder.Entity<OutboxDbEntity>(entity =>
        {
            entity.ToTable("outbox");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EventType).HasColumnName("event_type");
            entity.Property(e => e.PayloadJson).HasColumnName("payload_json");
            entity.Property(e => e.DedupKey).HasColumnName("dedup_key");
            entity.Property(e => e.Attempts).HasColumnName("attempts");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.LastAttemptAt).HasColumnName("last_attempt_at");
            entity.Property(e => e.LastError).HasColumnName("last_error");
            entity.HasIndex(e => e.DedupKey).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        // ── ForegroundEvents (T05/T07) ─────────────────────────────────────
        modelBuilder.Entity<ForegroundEventDbEntity>(entity =>
        {
            entity.ToTable("foreground_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AppId).HasColumnName("app_id");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.ServerDate).HasColumnName("server_date");
            entity.Property(e => e.Source).HasColumnName("source");
            entity.HasIndex(e => e.ServerDate);
            entity.HasIndex(e => new { e.AppId, e.ServerDate });
        });

        // ── ReconciliationHistory (T07) ───────────────────────────────────
        modelBuilder.Entity<ReconciliationHistoryDbEntity>(entity =>
        {
            entity.ToTable("reconciliation_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ServerDate).HasColumnName("server_date");
            entity.Property(e => e.AppsReconciled).HasColumnName("apps_reconciled");
            entity.Property(e => e.AppsBackfilled).HasColumnName("apps_backfilled");
            entity.Property(e => e.DiscrepanciesFound).HasColumnName("discrepancies_found");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.HasIndex(e => e.ServerDate).IsUnique();
        });

        // ── Consent (T25) ──────────────────────────────────────────────────
        modelBuilder.Entity<ConsentDbEntity>(entity =>
        {
            entity.ToTable("consent");
            entity.HasKey(e => e.DeviceId);
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.GrantedAt).HasColumnName("granted_at");
            entity.Property(e => e.GrantedByDeviceId).HasColumnName("granted_by_device_id");
        });
    }
}
