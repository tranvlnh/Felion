using Felion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.IntegrationTests;

public sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    public const string ConnectionStringEnvironmentVariable = "FELION_POSTGRES_TEST_CONNECTION";

    private PostgreSqlTestDatabase(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PostgreSqlTestDatabase> CreateAsync(CancellationToken cancellationToken = default)
    {
        var configuredConnection = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
            ?? throw new InvalidOperationException($"{ConnectionStringEnvironmentVariable} is required.");
        var builder = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = $"felion_test_{Guid.NewGuid():N}",
            Pooling = false
        };
        var database = new PostgreSqlTestDatabase(builder.ConnectionString);

        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        return database;
    }

    public FelionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FelionDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new FelionDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}
