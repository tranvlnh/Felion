using Microsoft.EntityFrameworkCore;

namespace Felion.Infrastructure.Persistence;

/// <summary>
/// The EF Core context for Felion's PostgreSQL database.
/// </summary>
public sealed class FelionDbContext(DbContextOptions<FelionDbContext> options) : DbContext(options)
{
}
