namespace ControlParental.Service;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

internal static class ControlParentalSchemaVersion
{
    internal const string Name = "offline-sync-recovery";
    internal const int Current = 1;
    internal static readonly string Checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        "outbox:v1|status:INTEGER NOT NULL DEFAULT 0|operation_id:TEXT NOT NULL DEFAULT ''|claim_version:INTEGER NOT NULL DEFAULT 0|claimed_until:TEXT NULL|next_eligible_at:TEXT NULL|dead_lettered_at:TEXT NULL|safe_failure_code:TEXT NULL|audit_reference:TEXT NULL|indexes:dedup_key UNIQUE,created_at,status,next_eligible_at,created_at,operation_id")));
}

internal static class SqliteSchemaBootstrapper
{
    private static readonly (string Name, string Type, long NotNull, string? Default, long PrimaryKey)[] BaseColumns =
    [
        ("id", "INTEGER", 1, null, 1),
        ("event_type", "TEXT", 1, null, 0),
        ("payload_json", "TEXT", 1, null, 0),
        ("dedup_key", "TEXT", 1, null, 0),
        ("attempts", "INTEGER", 1, null, 0),
        ("created_at", "TEXT", 1, null, 0),
        ("last_attempt_at", "TEXT", 0, null, 0),
        ("last_error", "TEXT", 0, null, 0),
    ];

    private static readonly (string Name, string Type, long NotNull, string? Default, long PrimaryKey)[] LegacyBaseColumns =
    [
        ("id", "INTEGER", 0, null, 1),
        ("event_type", "TEXT", 1, null, 0),
        ("payload_json", "TEXT", 1, null, 0),
        ("dedup_key", "TEXT", 1, null, 0),
        ("attempts", "INTEGER", 1, "0", 0),
        ("created_at", "TEXT", 1, null, 0),
        ("last_attempt_at", "TEXT", 0, null, 0),
        ("last_error", "TEXT", 0, null, 0),
    ];

    private static readonly (string Name, bool Unique, string[] Columns)[] RequiredIndexes =
    [
        ("IX_outbox_dedup_key", true, ["dedup_key"]),
        ("IX_outbox_created_at", false, ["created_at"]),
        ("IX_outbox_status_next_eligible_at_created_at", false, ["status", "next_eligible_at", "created_at"]),
        ("IX_outbox_operation_id", false, ["operation_id"]),
    ];

    private static readonly (string Name, string Type, long NotNull, string Default)[] LifecycleColumns =
    [
        ("status", "INTEGER", 1, "0"),
        ("operation_id", "TEXT", 1, "''"),
        ("claim_version", "INTEGER", 1, "0"),
        ("claimed_until", "TEXT", 0, null!),
        ("next_eligible_at", "TEXT", 0, null!),
        ("dead_lettered_at", "TEXT", 0, null!),
        ("safe_failure_code", "TEXT", 0, null!),
        ("audit_reference", "TEXT", 0, null!),
    ];

    public static async Task AdoptAsync(ControlParentalDbContext db, CancellationToken cancellationToken = default)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var hasCurrentMarker = await ValidateRecordedSchemaVersionAsync(connection, transaction, cancellationToken);
            var tableInfo = await ReadTableInfoAsync(connection, transaction, "outbox", cancellationToken);
            var columns = tableInfo.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            ValidateBaseSchema(tableInfo);
            if (hasCurrentMarker)
            {
                ValidateCurrentPhysicalSchema(columns);
                await ValidateLifecycleColumnsAsync(connection, transaction, cancellationToken);
                await ValidateIndexesAsync(connection, transaction, cancellationToken, requireAll: true);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var lifecycleCount = LifecycleColumns.Count(column => columns.Contains(column.Name));
            if ((lifecycleCount == 1 && !columns.Contains("status")) || (lifecycleCount > 1 && lifecycleCount != LifecycleColumns.Length))
                throw new InvalidOperationException("SQLite outbox lifecycle schema is incomplete.");
            if (lifecycleCount == LifecycleColumns.Length)
                await ValidateLifecycleColumnsAsync(connection, transaction, cancellationToken);
            await ValidateIndexesAsync(connection, transaction, cancellationToken, requireAll: false);
            await AddLifecycleColumnsAsync(connection, transaction, columns, cancellationToken);
            await ValidateLifecycleColumnsAsync(connection, transaction, cancellationToken);
            await ExecuteAsync(connection, transaction, "UPDATE outbox SET operation_id = dedup_key WHERE operation_id IS NULL OR operation_id = '';", cancellationToken);
            await ExecuteAsync(connection, transaction, "CREATE UNIQUE INDEX IF NOT EXISTS IX_outbox_dedup_key ON outbox(dedup_key);", cancellationToken);
            await ExecuteAsync(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_outbox_created_at ON outbox(created_at);", cancellationToken);
            await ExecuteAsync(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_outbox_status_next_eligible_at_created_at ON outbox(status, next_eligible_at, created_at);", cancellationToken);
            await ExecuteAsync(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_outbox_operation_id ON outbox(operation_id);", cancellationToken);
            await ValidateIndexesAsync(connection, transaction, cancellationToken, requireAll: true);
            await ExecuteAsync(connection, transaction, "CREATE TABLE IF NOT EXISTS schema_version (name TEXT NOT NULL PRIMARY KEY, version INTEGER NOT NULL, checksum TEXT NOT NULL);", cancellationToken);
            await InsertCurrentSchemaVersionAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new InvalidOperationException("SQLite schema adoption failed; hosted work was not started.");
        }
    }

    private static void ValidateBaseSchema(IReadOnlyDictionary<string, (string Type, long NotNull, string? Default, long PrimaryKey)> actual)
    {
        if (!MatchesBaseDefinition(actual, BaseColumns) && !MatchesBaseDefinition(actual, LegacyBaseColumns))
            throw new InvalidOperationException("SQLite outbox base schema is incompatible.");
    }

    private static bool MatchesBaseDefinition(IReadOnlyDictionary<string, (string Type, long NotNull, string? Default, long PrimaryKey)> actual, IReadOnlyList<(string Name, string Type, long NotNull, string? Default, long PrimaryKey)> expectedDefinition)
    {
        foreach (var expected in expectedDefinition)
        {
            if (!actual.TryGetValue(expected.Name, out var value) ||
                !string.Equals(value.Type, expected.Type, StringComparison.OrdinalIgnoreCase) ||
                value.NotNull != expected.NotNull ||
                value.Default != expected.Default ||
                value.PrimaryKey != expected.PrimaryKey)
                return false;
        }

        return true;
    }

    private static void ValidateCurrentPhysicalSchema(ISet<string> columns)
    {
        if (columns.Count != BaseColumns.Length + LifecycleColumns.Length ||
            BaseColumns.Any(column => !columns.Contains(column.Name)) ||
            LifecycleColumns.Any(column => !columns.Contains(column.Name)))
            throw new InvalidOperationException("SQLite outbox schema is incompatible with the current marker.");
    }

    private static async Task AddLifecycleColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, ISet<string> columns, CancellationToken ct)
    {
        foreach (var column in LifecycleColumns)
        {
            if (columns.Contains(column.Name)) continue;
            var definition = column.NotNull == 1 ? $"{column.Type} NOT NULL DEFAULT {column.Default}" : $"{column.Type} NULL";
            await ExecuteAsync(connection, transaction, $"ALTER TABLE outbox ADD COLUMN {column.Name} {definition};", ct);
            columns.Add(column.Name);
        }
    }

    private static async Task ValidateLifecycleColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
    {
        var actual = await ReadTableInfoAsync(connection, transaction, "outbox", ct);
        foreach (var expected in LifecycleColumns)
        {
            if (!actual.TryGetValue(expected.Name, out var value) || !string.Equals(value.Type, expected.Type, StringComparison.OrdinalIgnoreCase) || value.NotNull != expected.NotNull || (expected.Default != null && value.Default != expected.Default) || (expected.Default == null && value.Default != null))
                throw new InvalidOperationException("SQLite outbox lifecycle schema is incompatible.");
        }
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
        => (await ReadTableInfoAsync(connection, transaction, "outbox", ct)).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static async Task<Dictionary<string, (string Type, long NotNull, string? Default, long PrimaryKey)>> ReadTableInfoAsync(SqliteConnection connection, SqliteTransaction transaction, string table, CancellationToken ct)
    {
        var result = new Dictionary<string, (string, long, string?, long)>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(1)] = (reader.GetString(2), reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt64(5));
        return result;
    }

    private static async Task<bool> ValidateRecordedSchemaVersionAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_version';";
        if (Convert.ToInt64(await exists.ExecuteScalarAsync(ct)) == 0) return false;

        var columns = await ReadTableInfoAsync(connection, transaction, "schema_version", ct);
        if (columns.Count != 3 || !columns.TryGetValue("name", out var name) || name.Type != "TEXT" || name.NotNull != 1 || name.PrimaryKey != 1 || !columns.TryGetValue("version", out var version) || version.Type != "INTEGER" || version.NotNull != 1 || version.PrimaryKey != 0 || !columns.TryGetValue("checksum", out var checksum) || checksum.Type != "TEXT" || checksum.NotNull != 1 || checksum.PrimaryKey != 0)
            throw new InvalidOperationException("SQLite schema version metadata is incompatible.");

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT version, checksum FROM schema_version WHERE name=@name;";
        command.Parameters.AddWithValue("@name", ControlParentalSchemaVersion.Name);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var count = 0;
        while (await reader.ReadAsync(ct))
        {
            count++;
            if (count != 1 || reader.IsDBNull(0) || reader.GetFieldType(0) != typeof(long) || reader.GetInt64(0) != ControlParentalSchemaVersion.Current || reader.IsDBNull(1) || reader.GetFieldType(1) != typeof(string) || reader.GetString(1) != ControlParentalSchemaVersion.Checksum)
                throw new InvalidOperationException("SQLite schema version is unsupported.");
        }

        return count == 1;
    }

    private static async Task ValidateIndexesAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct, bool requireAll)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var existing = new List<(string Name, bool Unique)>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA index_list(outbox);";
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                existing.Add((reader.GetString(1), reader.GetInt64(2) == 1));
        }

        foreach (var index in existing)
        {
            var expected = RequiredIndexes.FirstOrDefault(item => item.Name == index.Name);
            if (expected.Name == null)
                throw new InvalidOperationException("SQLite outbox indexes are incompatible.");
            if (index.Unique == expected.Unique && await IndexColumnsMatchAsync(connection, transaction, index.Name, expected.Columns, ct))
                found.Add(index.Name);
            else
                throw new InvalidOperationException("SQLite outbox indexes are incompatible.");
        }

        if (requireAll && RequiredIndexes.Any(index => !found.Contains(index.Name)))
            throw new InvalidOperationException("SQLite outbox indexes are incomplete.");
    }

    private static async Task<bool> IndexColumnsMatchAsync(SqliteConnection connection, SqliteTransaction transaction, string name, IReadOnlyList<string> expected, CancellationToken ct)
    {
        var actual = new List<string>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA index_info('{name}');";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            actual.Add(reader.GetString(2));
        return actual.SequenceEqual(expected, StringComparer.Ordinal);
    }

    private static async Task InsertCurrentSchemaVersionAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO schema_version(name, version, checksum) VALUES (@name, @version, @checksum) ON CONFLICT(name) DO NOTHING;";
        command.Parameters.AddWithValue("@name", ControlParentalSchemaVersion.Name);
        command.Parameters.AddWithValue("@version", ControlParentalSchemaVersion.Current);
        command.Parameters.AddWithValue("@checksum", ControlParentalSchemaVersion.Checksum);
        await command.ExecuteNonQueryAsync(ct);
        await ValidateRecordedSchemaVersionAsync(connection, transaction, ct);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
