namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Shared SQLite admission fixture owned by child 1.1A and reused by B1/B2.
/// </summary>
public abstract class OutboxManagerTestFixture : IDisposable
{
    protected static readonly DateTimeOffset FixedNow = new(2032, 1, 2, 3, 4, 5, TimeSpan.Zero);

    protected readonly ControlParentalDbContext db;
    protected readonly SqliteConnection connection;
    protected readonly SqliteConnection secondConnection;
    protected readonly OutboxManager manager;
    protected readonly OutboxManager secondManager;
    protected readonly FakeTimeProvider timeProvider;
    protected readonly IDbContextFactory<ControlParentalDbContext> dbContextFactory;

    protected OutboxManagerTestFixture()
    {
        var databaseName = $"outbox-tests-{Guid.NewGuid():N}";
        this.connection = new SqliteConnection($"Data Source=file:{databaseName}?mode=memory&cache=shared;Default Timeout=1");
        this.connection.Open();
        this.secondConnection = new SqliteConnection($"Data Source=file:{databaseName}?mode=memory&cache=shared;Default Timeout=1");
        this.secondConnection.Open();

        var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite(this.connection)
            .Options;
        this.db = new ControlParentalDbContext(options);
        this.db.Database.EnsureCreated();
        this.timeProvider = new FakeTimeProvider(FixedNow);
        this.dbContextFactory = new TrackingDbContextFactory(options);
        this.manager = new OutboxManager(this.dbContextFactory, this.timeProvider);
        var secondOptions = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite(this.secondConnection)
            .Options;
        this.secondManager = new OutboxManager(new TrackingDbContextFactory(secondOptions), this.timeProvider);
    }

    public void Dispose()
    {
        this.db.Dispose();
        this.connection.Dispose();
        this.secondConnection.Dispose();
    }

    protected async Task SeedEntriesAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await this.manager.EnqueueAsync("events", new { Value = i }, $"operation-{i}");
        }
    }

    protected sealed class FakeTimeProvider : ITimeProvider
    {
        public FakeTimeProvider(DateTimeOffset wallClock) => this.WallClockNow = wallClock;
        public long MonotonicNow => 0;
        public DateTimeOffset WallClockNow { get; private set; }
        public TimeZoneInfo CurrentZone => TimeZoneInfo.Utc;
        public DateOnly? ServerDate => DateOnly.FromDateTime(this.WallClockNow.UtcDateTime);
        public bool IsServerDateUncertain => false;
        public event EventHandler<TimeChangedEventArgs>? TimeChanged;
        public void SetServerDate(long offsetMs) { }
        public bool DetectClockJump() => false;
        public void SetWallClockNow(DateTimeOffset now) => this.WallClockNow = now;
    }
}
