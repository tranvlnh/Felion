using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Felion.Infrastructure.Persistence;

public sealed class FelionDbContextFactory : IDesignTimeDbContextFactory<FelionDbContext>
{
    public FelionDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FELION_DESIGN_TIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=felion;Username=felion;Password=design-time-only";

        var options = new DbContextOptionsBuilder<FelionDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(FelionDbContext).Assembly.FullName))
            .Options;

        return new FelionDbContext(options);
    }
}
