// <copyright file="OutboxManagerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class OutboxManagerTests : OutboxManagerTestFixture
{
    [Fact]
    public async Task EnqueueAsync_ValidPayload_EnqueuesEntry()
    {
        // Arrange
        var payload = new { Message = "test" };
        var dedupKey = Guid.NewGuid().ToString();

        // Act
        await this.manager.EnqueueAsync("usage_logs", payload, dedupKey);

        // Assert
        var count = await this.db.Outbox.CountAsync();
        Assert.Equal(1, count);

        var entry = await this.db.Outbox.FirstAsync();
        Assert.Equal("usage_logs", entry.EventType);
        Assert.Contains("test", entry.PayloadJson);
        Assert.Equal(dedupKey, entry.DedupKey);
        Assert.Equal(0, entry.Attempts);
        Assert.Equal(FixedNow, entry.CreatedAt);
    }

    [Fact]
    public async Task EnqueueAsync_DuplicateDedupKey_IgnoresDuplicate()
    {
        // Arrange
        var payload = new { Message = "test" };
        var dedupKey = Guid.NewGuid().ToString();

        // First enqueue succeeds
        await this.manager.EnqueueAsync("usage_logs", payload, dedupKey);

        // Second enqueue with same dedupKey - InMemory provider doesn't throw
        // like SQLite, but the manager handles gracefully
        await this.manager.EnqueueAsync("usage_logs", payload, dedupKey);

        // Assert - SQLite enforces the unique key and the manager keeps the first row
        var count = await this.db.Outbox.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetPendingEntriesAsync_ReturnsOrderedByCreatedAt()
    {
        // Arrange
        await this.db.Outbox.AddRangeAsync(
            new OutboxDbEntity { EventType = "a", PayloadJson = "{}", DedupKey = "k1", CreatedAt = FixedNow.AddMinutes(-2) },
            new OutboxDbEntity { EventType = "b", PayloadJson = "{}", DedupKey = "k2", CreatedAt = FixedNow.AddMinutes(-1) },
            new OutboxDbEntity { EventType = "c", PayloadJson = "{}", DedupKey = "k3", CreatedAt = FixedNow });
        await this.db.SaveChangesAsync();

        // Act
        var entries = await this.manager.GetPendingEntriesAsync();

        // Assert
        Assert.Equal(3, entries.Count);
        Assert.Equal("a", entries[0].TableName);
        Assert.Equal("b", entries[1].TableName);
        Assert.Equal("c", entries[2].TableName);
    }

    [Fact]
    public async Task GetPendingEntriesAsync_RespectsLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await this.db.Outbox.AddAsync(new OutboxDbEntity
            {
                EventType = $"type_{i}",
                PayloadJson = "{}",
                DedupKey = $"key_{i}",
                CreatedAt = FixedNow.AddMinutes(i),
            });
        }

        await this.db.SaveChangesAsync();

        // Act
        var entries = await this.manager.GetPendingEntriesAsync(limit: 5);

        // Assert
        Assert.Equal(5, entries.Count);
    }

    [Fact]
    public async Task GetPendingCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await this.db.Outbox.AddAsync(new OutboxDbEntity
            {
                EventType = $"type_{i}",
                PayloadJson = "{}",
                DedupKey = $"key_{i}",
                CreatedAt = FixedNow,
            });
        }

        await this.db.SaveChangesAsync();

        // Act
        var count = await this.manager.GetPendingCountAsync();

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task EnqueueIntegrityNotificationAsync_CreatesEntryWithCorrectPayload()
    {
        // Arrange
        var timestamp = FixedNow;
        var notificationType = "integrity_warning";
        var title = "Test Title";
        var body = "Test Body";

        // Act
        await this.manager.EnqueueIntegrityNotificationAsync(
            notificationType, title, body, timestamp);

        // Assert
        var entries = await this.manager.GetPendingEntriesAsync();
        Assert.Single(entries);

        var entry = entries[0];
        Assert.Equal("notifications", entry.TableName);
        Assert.Contains("integrity_warning", entry.PayloadJson);
        Assert.Contains("Test Title", entry.PayloadJson);
        Assert.Contains("Test Body", entry.PayloadJson);
        Assert.Equal($"integrity_integrity_warning_{timestamp.ToUnixTimeMilliseconds()}", entry.DedupKey);
    }

    [Fact]
    public async Task EnqueueIntegrityNotificationAsync_ExplicitKeyIsPersistedVerbatim()
    {
        var key = "integrity/device-a/integrity-binary/8/13/notification";

        await this.manager.EnqueueIntegrityNotificationAsync(
            "integrity_degrade_pending",
            "Test Title",
            "Test Body",
            FixedNow,
            key,
            CancellationToken.None);

        var entry = (await this.manager.GetPendingEntriesAsync()).Single();
        Assert.Equal(key, entry.DedupKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EnqueueIntegrityNotificationAsync_ExplicitKeyRejectsNullOrEmptyBeforePersistence(string? key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => this.manager.EnqueueIntegrityNotificationAsync(
            "integrity_warning", "Test Title", "Test Body", FixedNow, key!, CancellationToken.None));
        Assert.Empty(await this.manager.GetPendingEntriesAsync());
    }

    [Fact]
    public async Task EnqueueIntegrityNotificationAsync_LegacyFiveArgumentDefaultBindsLegacyOverload()
    {
        await this.manager.EnqueueIntegrityNotificationAsync("integrity_warning", "Test Title", "Test Body", FixedNow, default);
        var entry = Assert.Single(await this.manager.GetPendingEntriesAsync());
        Assert.Equal($"integrity_integrity_warning_{FixedNow.ToUnixTimeMilliseconds()}", entry.DedupKey);
    }

    [Fact]
    public async Task EnqueueAsync_NullPayload_SerializesToNull()
    {
        // Arrange
        var dedupKey = Guid.NewGuid().ToString();

        // Act - JsonSerializer serializes null as "null" string
        await this.manager.EnqueueAsync("usage_logs", null!, dedupKey);

        // Assert
        var entry = await this.db.Outbox.FirstAsync();
        Assert.Equal("null", entry.PayloadJson);
    }

    [Fact]
    public async Task GetPendingEntriesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var entries = await this.manager.GetPendingEntriesAsync();

        // Assert
        Assert.Empty(entries);
    }

}
