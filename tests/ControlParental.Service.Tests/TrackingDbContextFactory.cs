namespace ControlParental.Service.Tests;

using Microsoft.EntityFrameworkCore;

internal sealed class TrackingDbContextFactory : IDbContextFactory<ControlParentalDbContext>
{
    private readonly DbContextOptions<ControlParentalDbContext> options;

    public TrackingDbContextFactory(DbContextOptions<ControlParentalDbContext> options)
    {
        this.options = options;
    }

    public int CreateCount { get; private set; }

    public ControlParentalDbContext CreateDbContext()
    {
        this.CreateCount++;
        return new ControlParentalDbContext(this.options);
    }
}
