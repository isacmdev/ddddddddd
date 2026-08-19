namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

public sealed class SchemaAdoptionTests
{
    private const string KnownSchemaChecksum = "D79AA6FDC58AF83F72F5455BBAA3A7D88429728BB511F02307121F9BBEBA8963";
    private static readonly string[] RequiredColumns =
    [
        "id", "event_type", "payload_json", "dedup_key", "attempts", "created_at",
        "last_attempt_at", "last_error", "status", "operation_id", "claim_version",
        "claimed_until", "next_eligible_at", "dead_lettered_at", "safe_failure_code", "audit_reference",
    ];

    [Fact]
    public async Task FreshProductionModel_EnsureCreated_CreatesCompleteOutboxSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ControlParentalDbContext(new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite(connection)
            .UseModel(ControlParental.Service.CompiledModels.ControlParentalDbContextModel.Instance)
            .Options);

        await db.Database.EnsureCreatedAsync();

        var columns = await ReadColumnsAsync(connection);
        Assert.All(RequiredColumns, column => Assert.Contains(column, columns.Keys));
        Assert.Equal("INTEGER", columns["status"].Type);
        Assert.Equal("0", columns["status"].DefaultValue);
        Assert.Equal("INTEGER", columns["claim_version"].Type);
        Assert.Equal("0", columns["claim_version"].DefaultValue);
        Assert.Equal("TEXT", columns["operation_id"].Type);
        Assert.Equal("''", columns["operation_id"].DefaultValue);
        Assert.Equal("TEXT", columns["claimed_until"].Type);
        Assert.Equal("TEXT", columns["next_eligible_at"].Type);
        Assert.Equal("TEXT", columns["dead_lettered_at"].Type);
        Assert.Equal("TEXT", columns["safe_failure_code"].Type);
        Assert.Equal("TEXT", columns["audit_reference"].Type);
        var indexes = await ReadIndexesAsync(connection);
        Assert.Contains("IX_outbox_dedup_key", indexes);
        Assert.Contains("IX_outbox_created_at", indexes);
        Assert.Contains("IX_outbox_status_next_eligible_at_created_at", indexes);
        Assert.Contains("IX_outbox_operation_id", indexes);
        var detailed = await ReadDetailedColumnsAsync(connection);
        Assert.Equal(1, detailed["status"].NotNull);
        Assert.Equal(1, detailed["operation_id"].NotNull);
        Assert.Equal(1, detailed["claim_version"].NotNull);
        Assert.Equal(0, detailed["claimed_until"].NotNull);
        Assert.Equal(0, detailed["next_eligible_at"].NotNull);
        Assert.Equal(0, detailed["dead_lettered_at"].NotNull);
        Assert.Equal(0, detailed["safe_failure_code"].NotNull);
        Assert.Equal(0, detailed["audit_reference"].NotNull);
        var definitions = await ReadIndexDefinitionsAsync(connection);
        AssertIndex(definitions, "IX_outbox_dedup_key", true, "dedup_key");
        AssertIndex(definitions, "IX_outbox_created_at", false, "created_at");
        AssertIndex(definitions, "IX_outbox_status_next_eligible_at_created_at", false, "status", "next_eligible_at", "created_at");
        AssertIndex(definitions, "IX_outbox_operation_id", false, "operation_id");
    }

    [Fact]
    public async Task LegacySchema_AdoptionBackfillsAndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE usage_today (app_id INTEGER NOT NULL, server_date TEXT NOT NULL, elapsed_seconds INTEGER NOT NULL DEFAULT 0, minutes INTEGER NOT NULL DEFAULT 0, last_updated TEXT NOT NULL, PRIMARY KEY(app_id, server_date));");
        await ExecuteAsync(connection, "CREATE TABLE outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL, status INTEGER NOT NULL DEFAULT 0);");
        await ExecuteAsync(connection, "INSERT INTO outbox(event_type, payload_json, dedup_key, created_at) VALUES ('legacy', '{}', 'legacy-key', '2032-01-02T03:04:05+00:00');");
        await using var db = NewDb(connection);

        await SqliteSchemaBootstrapper.AdoptAsync(db);
        var firstSchema = await ReadSchemaFingerprintAsync(connection);
        await SqliteSchemaBootstrapper.AdoptAsync(db);
        var secondSchema = await ReadSchemaFingerprintAsync(connection);

        Assert.Equal(firstSchema, secondSchema);
        Assert.Equal(1, await ExecuteScalarAsync<long>(connection, "SELECT COUNT(*) FROM outbox WHERE operation_id = dedup_key AND claim_version = 0;"));
        Assert.Equal("legacy-key", await ExecuteScalarAsync<string>(connection, "SELECT operation_id FROM outbox WHERE id = 1;"));
        Assert.Contains("IX_outbox_status_next_eligible_at_created_at", await ReadIndexesAsync(connection));
        Assert.Contains("IX_outbox_operation_id", await ReadIndexesAsync(connection));
        Assert.Equal(1, await ExecuteScalarAsync<long>(connection, "SELECT version FROM schema_version WHERE name = 'offline-sync-recovery';"));
        Assert.Equal(KnownSchemaChecksum, await ExecuteScalarAsync<string>(connection, "SELECT checksum FROM schema_version WHERE name = 'offline-sync-recovery';"));
    }

    [Fact]
    public async Task FailedAdoption_RollsBackWithoutMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE usage_today (app_id INTEGER NOT NULL, server_date TEXT NOT NULL, elapsed_seconds INTEGER NOT NULL DEFAULT 0, minutes INTEGER NOT NULL DEFAULT 0, last_updated TEXT NOT NULL, PRIMARY KEY(app_id, server_date));");
        await ExecuteAsync(connection, "CREATE TABLE outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL, status INTEGER NOT NULL DEFAULT 0);");
        await ExecuteAsync(connection, "INSERT INTO outbox(event_type, payload_json, dedup_key, created_at) VALUES ('legacy', '{}', 'duplicate', '2032-01-02T03:04:05+00:00'), ('legacy', '{}', 'duplicate', '2032-01-02T03:04:06+00:00');");
        await using var db = NewDb(connection);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Program.InitializeDatabaseAsync(db));
        Assert.DoesNotContain("operation_id", (await ReadColumnsAsync(connection)).Keys);
        Assert.Equal(0, await ExecuteScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_version';"));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("'future'")]
    public async Task UnsupportedRecordedVersion_IsRejectedBeforeMutation(string version)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE schema_version (name TEXT NOT NULL PRIMARY KEY, version INTEGER NOT NULL, checksum TEXT NOT NULL);");
         await ExecuteAsync(connection, $"INSERT INTO schema_version(name, version, checksum) VALUES ('offline-sync-recovery', {version}, 'bad');");
        var before = await ReadOutboxDefinitionFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadOutboxDefinitionFingerprintAsync(connection));
    }

    [Fact]
    public async Task PartialLifecycleSchema_IsRejectedBeforeAnyDdl()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL, status INTEGER NOT NULL DEFAULT 0, operation_id TEXT NOT NULL DEFAULT '');");
        var before = await ReadOutboxDefinitionFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadOutboxDefinitionFingerprintAsync(connection));
    }

    [Fact]
    public async Task ExistingWrongNamedIndex_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE INDEX IX_outbox_dedup_key ON outbox(created_at);");
        var before = await ReadOutboxDefinitionFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadOutboxDefinitionFingerprintAsync(connection));
    }

    [Fact]
    public async Task UnexpectedIndex_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE INDEX IX_outbox_unexpected ON outbox(payload_json);");
        var before = await ReadOutboxDefinitionFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadOutboxDefinitionFingerprintAsync(connection));
    }

    [Fact]
    public async Task MissingChecksumColumn_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE schema_version (name TEXT NOT NULL PRIMARY KEY, version INTEGER NOT NULL);");
        var before = await ReadOutboxDefinitionFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadOutboxDefinitionFingerprintAsync(connection));
    }

    [Fact]
    public async Task NullChecksum_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE schema_version (name TEXT NOT NULL PRIMARY KEY, version INTEGER NOT NULL, checksum TEXT NULL);");
        await ExecuteAsync(connection, "INSERT INTO schema_version(name, version, checksum) VALUES ('offline-sync-recovery', 1, NULL);");
        var before = await ReadDatabaseFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadDatabaseFingerprintAsync(connection));
    }

    [Fact]
    public async Task Cancellation_PreservesOperationCanceledExceptionAndDoesNotMutate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection), cancellation.Token));

        Assert.DoesNotContain("operation_id", (await ReadColumnsAsync(connection)).Keys);
    }

    [Fact]
    public async Task NullRecordedVersion_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE schema_version (name TEXT NOT NULL PRIMARY KEY, version INTEGER NULL, checksum TEXT NOT NULL);");
        await ExecuteAsync(connection, "INSERT INTO schema_version(name, version, checksum) VALUES ('offline-sync-recovery', NULL, 'bad');");
        var before = await ReadDatabaseFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadDatabaseFingerprintAsync(connection));
    }

    [Fact]
    public async Task MalformedVersionTable_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE schema_version (name TEXT, version INTEGER, checksum TEXT, extra TEXT);");
        var before = await ReadDatabaseFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadDatabaseFingerprintAsync(connection));
    }

    [Fact]
    public async Task MissingTargetRow_IsAdoptedAndUnrelatedRowsArePreserved()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE schema_version (name TEXT NOT NULL PRIMARY KEY, version INTEGER NOT NULL, checksum TEXT NOT NULL);");
        await ExecuteAsync(connection, "INSERT INTO schema_version(name, version, checksum) VALUES ('other-schema', 42, 'other');");

        await SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection));

        Assert.Equal(42, await ExecuteScalarAsync<long>(connection, "SELECT version FROM schema_version WHERE name = 'other-schema';"));
        Assert.Equal(1, await ExecuteScalarAsync<long>(connection, "SELECT version FROM schema_version WHERE name = 'offline-sync-recovery';"));
    }

    [Fact]
    public async Task DuplicateTargetRows_AreRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE schema_version (name TEXT NOT NULL, version INTEGER NOT NULL, checksum TEXT NOT NULL);");
        await ExecuteAsync(connection, "INSERT INTO schema_version(name, version, checksum) VALUES ('offline-sync-recovery', 1, 'bad'), ('offline-sync-recovery', 1, 'bad');");
        var before = string.Join('|', (await ReadColumnsAsync(connection)).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Type}:{pair.Value.DefaultValue}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, string.Join('|', (await ReadColumnsAsync(connection)).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Type}:{pair.Value.DefaultValue}")));
    }

    [Fact]
    public async Task CurrentMarker_WithWrongChecksum_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyOutboxAsync(connection);
        await ExecuteAsync(connection, "CREATE TABLE schema_version (name TEXT NOT NULL PRIMARY KEY, version INTEGER NOT NULL, checksum TEXT NOT NULL);");
        await ExecuteAsync(connection, "INSERT INTO schema_version(name, version, checksum) VALUES ('offline-sync-recovery', 1, 'BAD');");
        var before = await ReadDatabaseFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadDatabaseFingerprintAsync(connection));
    }

    [Fact]
    public async Task ExistingLifecycleColumn_WithWrongShape_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL, status TEXT NULL DEFAULT 'bad');");
        var before = string.Join('|', (await ReadColumnsAsync(connection)).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Type}:{pair.Value.DefaultValue}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, string.Join('|', (await ReadColumnsAsync(connection)).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Type}:{pair.Value.DefaultValue}")));
    }

    [Fact]
    public async Task CompleteCurrentSchema_WithWrongLifecycleShape_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL, status TEXT NULL DEFAULT 'bad', operation_id TEXT NOT NULL DEFAULT '', claim_version INTEGER NOT NULL DEFAULT 0, claimed_until TEXT NULL, next_eligible_at TEXT NULL, dead_lettered_at TEXT NULL, safe_failure_code TEXT NULL, audit_reference TEXT NULL);");
        var before = await ReadOutboxDefinitionFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadOutboxDefinitionFingerprintAsync(connection));
    }

    [Fact]
    public async Task CurrentMarker_WithStatusOnlySchema_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL, status INTEGER NOT NULL DEFAULT 0);");
        await CreateCurrentMarkerAsync(connection);
        var before = await ReadDatabaseFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadDatabaseFingerprintAsync(connection));
    }

    [Fact]
    public async Task CurrentMarker_WithMissingRequiredIndex_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateCurrentOutboxAsync(connection, includeOperationIndex: false);
        await CreateCurrentMarkerAsync(connection);
        var before = await ReadDatabaseFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadDatabaseFingerprintAsync(connection));
    }

    [Fact]
    public async Task CurrentMarker_WithWrongLifecycleShape_IsRejectedBeforeMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL, status TEXT NULL DEFAULT 'bad', operation_id TEXT NOT NULL DEFAULT '', claim_version INTEGER NOT NULL DEFAULT 0, claimed_until TEXT NULL, next_eligible_at TEXT NULL, dead_lettered_at TEXT NULL, safe_failure_code TEXT NULL, audit_reference TEXT NULL);");
        await ExecuteAsync(connection, "CREATE UNIQUE INDEX IX_outbox_dedup_key ON outbox(dedup_key);");
        await ExecuteAsync(connection, "CREATE INDEX IX_outbox_created_at ON outbox(created_at);");
        await ExecuteAsync(connection, "CREATE INDEX IX_outbox_status_next_eligible_at_created_at ON outbox(status, next_eligible_at, created_at);");
        await ExecuteAsync(connection, "CREATE INDEX IX_outbox_operation_id ON outbox(operation_id);");
        await CreateCurrentMarkerAsync(connection);
        var before = await ReadDatabaseFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadDatabaseFingerprintAsync(connection));
    }

    [Theory]
    [InlineData("attempts-type")]
    [InlineData("event-type-nullability")]
    [InlineData("attempts-default")]
    [InlineData("id-primary-key")]
    public async Task CurrentMarker_WithMalformedBaseDefinition_IsRejectedBeforeMutation(string mutation)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateCurrentOutboxAsync(connection, includeOperationIndex: true, mutation);
        await CreateCurrentMarkerAsync(connection);
        var before = await ReadDatabaseFingerprintAsync(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SqliteSchemaBootstrapper.AdoptAsync(NewDb(connection)));

        Assert.Equal(before, await ReadDatabaseFingerprintAsync(connection));
    }

    [Fact]
    public async Task FileBackedRestart_PreservesAdoptedSchemaDataAndNoOp()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdd6_schema_{Guid.NewGuid():N}.db");
        try
        {
            await using (var firstConnection = new SqliteConnection($"Data Source={path}"))
            {
                await firstConnection.OpenAsync();
                await CreateLegacyOutboxAsync(firstConnection);
                await ExecuteAsync(firstConnection, "INSERT INTO outbox(event_type, payload_json, dedup_key, created_at) VALUES ('legacy', '{}', 'restart-key', '2032-01-02T03:04:05+00:00');");
                await using var firstDb = NewDb(firstConnection);
                await SqliteSchemaBootstrapper.AdoptAsync(firstDb);
            }

            await using var secondConnection = new SqliteConnection($"Data Source={path}");
            await secondConnection.OpenAsync();
            await using var secondDb = NewDb(secondConnection);
            var before = await ReadDatabaseFingerprintAsync(secondConnection);
            await SqliteSchemaBootstrapper.AdoptAsync(secondDb);
            Assert.Equal(before, await ReadDatabaseFingerprintAsync(secondConnection));
            Assert.Equal("restart-key", await ExecuteScalarAsync<string>(secondConnection, "SELECT operation_id FROM outbox WHERE id = 1;"));
            Assert.Equal(KnownSchemaChecksum, await ExecuteScalarAsync<string>(secondConnection, "SELECT checksum FROM schema_version WHERE name = 'offline-sync-recovery';"));
            await secondConnection.DisposeAsync();
        }
        finally
        {
            DeleteWhenReleased(path);
        }
    }

    [Fact]
    public async Task ProductionStartupBoundary_DoesNotStartHostedWorkWhenAdoptionFails()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdd6_startup_{Guid.NewGuid():N}.db");
        try
        {
            await using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            await ExecuteAsync(connection, "CREATE TABLE usage_today (app_id INTEGER NOT NULL, server_date TEXT NOT NULL, elapsed_seconds INTEGER NOT NULL DEFAULT 0, minutes INTEGER NOT NULL DEFAULT 0, last_updated TEXT NOT NULL, PRIMARY KEY(app_id, server_date));");
            await ExecuteAsync(connection, "CREATE TABLE outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL, status TEXT NULL DEFAULT 'bad');");

            var host = new HostBuilder().ConfigureServices(services =>
            {
                services.AddDbContextFactory<ControlParentalDbContext>(options => options.UseSqlite($"Data Source={path}"));
                services.AddSingleton<StartupProbe>();
                services.AddHostedService(sp => sp.GetRequiredService<StartupProbe>());
            }).Build();

            using (host)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => Program.StartHostAfterDatabaseInitializationAsync(host));
                Assert.False(host.Services.GetRequiredService<StartupProbe>().Started);
            }
            await connection.DisposeAsync();
        }
        finally
        {
            DeleteWhenReleased(path);
        }
    }

    [Fact]
    public async Task ProductionStartupBoundary_StartsHostedWorkAfterSuccessfulAdoption()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdd6_startup_ok_{Guid.NewGuid():N}.db");
        try
        {
            var host = new HostBuilder().ConfigureServices(services =>
            {
                services.AddDbContextFactory<ControlParentalDbContext>(options => options.UseSqlite($"Data Source={path}"));
                services.AddSingleton<StartupProbe>();
                services.AddHostedService(sp => sp.GetRequiredService<StartupProbe>());
            }).Build();
            using (host)
            {
                await Program.StartHostAfterDatabaseInitializationAsync(host);
                Assert.True(host.Services.GetRequiredService<StartupProbe>().Started);
                await host.StopAsync();
            }
        }
        finally
        {
            DeleteWhenReleased(path);
        }
    }

    private static ControlParentalDbContext NewDb(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<ControlParentalDbContext>().UseSqlite(connection).Options);

    private static async Task<Dictionary<string, (string Type, string? DefaultValue)>> ReadColumnsAsync(SqliteConnection connection)
    {
        var result = new Dictionary<string, (string, string?)>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(outbox);";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(1)] = (reader.GetString(2), reader.IsDBNull(4) ? null : reader.GetString(4));
        }

        return result;
    }

    private static async Task<Dictionary<string, (string Type, long NotNull, string? DefaultValue)>> ReadDetailedColumnsAsync(SqliteConnection connection)
    {
        var result = new Dictionary<string, (string, long, string?)>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(outbox);";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(1)] = (reader.GetString(2), reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetString(4));
        }

        return result;
    }

    private static async Task<HashSet<string>> ReadIndexesAsync(SqliteConnection connection)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA index_list(outbox);";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(1));
        }

        return result;
    }

    private static async Task<Dictionary<string, (bool Unique, string[] Columns)>> ReadIndexDefinitionsAsync(SqliteConnection connection)
    {
        var names = new List<(string Name, bool Unique)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA index_list(outbox);";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                names.Add((reader.GetString(1), reader.GetInt64(2) == 1));
        }

        var result = new Dictionary<string, (bool, string[])>(StringComparer.Ordinal);
        foreach (var index in names)
        {
            var columns = new List<string>();
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA index_info('{index.Name}');";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(2));
            result[index.Name] = (index.Unique, columns.ToArray());
        }

        return result;
    }

    private static void AssertIndex(Dictionary<string, (bool Unique, string[] Columns)> definitions, string name, bool unique, params string[] columns)
    {
        Assert.True(definitions.ContainsKey(name));
        Assert.Equal(unique, definitions[name].Unique);
        Assert.Equal(columns, definitions[name].Columns);
    }

    private static async Task<string> ReadSchemaFingerprintAsync(SqliteConnection connection)
        => string.Join('|', (await ReadColumnsAsync(connection)).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Type}:{pair.Value.DefaultValue}"));

    private static async Task<string> ReadOutboxDefinitionFingerprintAsync(SqliteConnection connection)
        => string.Join('|', (await ReadDetailedColumnsAsync(connection)).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Type}:{pair.Value.NotNull}:{pair.Value.DefaultValue}")) + $";indexes={string.Join(';', (await ReadIndexDefinitionsAsync(connection)).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Unique}:{string.Join(',', pair.Value.Columns)}"))}";

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static void DeleteWhenReleased(string path)
    {
        for (var attempt = 0; attempt < 20 && File.Exists(path); attempt++)
        {
            try { File.Delete(path); }
            catch (IOException) { Thread.Sleep(25); }
        }
    }

    private static async Task CreateLegacyOutboxAsync(SqliteConnection connection)
        => await ExecuteAsync(connection, "CREATE TABLE outbox (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL, status INTEGER NOT NULL DEFAULT 0);");

    private static async Task CreateCurrentOutboxAsync(SqliteConnection connection, bool includeOperationIndex, string? mutation = null)
    {
        var baseColumns = "id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, payload_json TEXT NOT NULL, dedup_key TEXT NOT NULL, attempts INTEGER NOT NULL, created_at TEXT NOT NULL, last_attempt_at TEXT NULL, last_error TEXT NULL";
        baseColumns = mutation switch
        {
            "attempts-type" => baseColumns.Replace("attempts INTEGER", "attempts TEXT", StringComparison.Ordinal),
            "event-type-nullability" => baseColumns.Replace("event_type TEXT NOT NULL", "event_type TEXT NULL", StringComparison.Ordinal),
            "attempts-default" => baseColumns.Replace("attempts INTEGER NOT NULL", "attempts INTEGER NOT NULL DEFAULT 7", StringComparison.Ordinal),
            "id-primary-key" => baseColumns.Replace("id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT", "id INTEGER NOT NULL", StringComparison.Ordinal),
            null => baseColumns,
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null),
        };
        await ExecuteAsync(connection, $"CREATE TABLE outbox ({baseColumns}, status INTEGER NOT NULL DEFAULT 0, operation_id TEXT NOT NULL DEFAULT '', claim_version INTEGER NOT NULL DEFAULT 0, claimed_until TEXT NULL, next_eligible_at TEXT NULL, dead_lettered_at TEXT NULL, safe_failure_code TEXT NULL, audit_reference TEXT NULL);");
        await ExecuteAsync(connection, "CREATE UNIQUE INDEX IX_outbox_dedup_key ON outbox(dedup_key);");
        await ExecuteAsync(connection, "CREATE INDEX IX_outbox_created_at ON outbox(created_at);");
        await ExecuteAsync(connection, "CREATE INDEX IX_outbox_status_next_eligible_at_created_at ON outbox(status, next_eligible_at, created_at);");
        if (includeOperationIndex)
            await ExecuteAsync(connection, "CREATE INDEX IX_outbox_operation_id ON outbox(operation_id);");
    }

    private static async Task CreateCurrentMarkerAsync(SqliteConnection connection)
    {
        await ExecuteAsync(connection, "CREATE TABLE schema_version (name TEXT NOT NULL PRIMARY KEY, version INTEGER NOT NULL, checksum TEXT NOT NULL);");
        await ExecuteAsync(connection, $"INSERT INTO schema_version(name, version, checksum) VALUES ('offline-sync-recovery', 1, '{KnownSchemaChecksum}');");
    }

    private static async Task<string> ReadDatabaseFingerprintAsync(SqliteConnection connection)
        => string.Join('|',
            (await ReadColumnsAsync(connection)).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Type}:{pair.Value.DefaultValue}")) +
           $";indexes={string.Join(';', (await ReadIndexDefinitionsAsync(connection)).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Unique}:{string.Join(',', pair.Value.Columns)}"))};data={await ExecuteScalarAsync<string>(connection, "SELECT COALESCE(group_concat(id || ':' || dedup_key, '|'), '') FROM outbox;")};schema={await ExecuteScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_version';")};marker={await ExecuteScalarAsync<string?>(connection, "SELECT COALESCE((SELECT name || ':' || version || ':' || checksum FROM schema_version WHERE name = 'offline-sync-recovery'), 'missing');")}";
}

file sealed class StartupProbe : IHostedService
{
    public bool Started { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.Started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
