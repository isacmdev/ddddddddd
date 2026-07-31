using System;
using System.IO;
using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "ControlParental",
    "controlparental.db");

Console.WriteLine($"[DbInspector] DB: {dbPath}");
Console.WriteLine($"[DbInspector] Exists: {File.Exists(dbPath)}");

if (!File.Exists(dbPath))
{
    Console.WriteLine("[DbInspector] DB not found.");
    return;
}

await using var connection = new SqliteConnection($"Data Source={dbPath}");
await connection.OpenAsync();

Console.WriteLine("\n=== Policies ===");
await using var policies = new SqliteCommand("SELECT device_id, version, last_updated FROM policies;", connection);
await using var reader = await policies.ExecuteReaderAsync();
var hasData = false;
while (await reader.ReadAsync())
{
    hasData = true;
    Console.WriteLine($"  device_id={reader.GetString(0)}, version={reader.GetInt32(1)}, updated={reader.GetDateTime(2)}");
}
if (!hasData) Console.WriteLine("  (no rows)");

Console.WriteLine("\n=== Outbox ===");
await using var outbox = new SqliteCommand("SELECT id, event_type, created_at, attempts FROM outbox LIMIT 20;", connection);
await using var odr = await outbox.ExecuteReaderAsync();
var outboxCount = 0;
while (await odr.ReadAsync())
{
    outboxCount++;
    Console.WriteLine($"  id={odr.GetInt32(0)}, event_type={odr.GetString(1)}, created_at={odr.GetDateTime(2)}, attempts={odr.GetInt32(3)}");
}
Console.WriteLine($"  Total: {outboxCount} rows");

Console.WriteLine("\n=== UsageToday ===");
await using var usage = new SqliteCommand("SELECT app_id, server_date, minutes FROM usage_today LIMIT 10;", connection);
await using var udr = await usage.ExecuteReaderAsync();
var usageCount = 0;
while (await udr.ReadAsync())
{
    usageCount++;
    Console.WriteLine($"  app={udr.GetString(0)}, date={udr.GetDateTime(1)}, min={udr.GetInt32(2)}");
}
Console.WriteLine($"  Total: {usageCount} rows");

Console.WriteLine("\n[DbInspector] Done.");
