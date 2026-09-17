namespace Felion.IntegrationTests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlTestDatabase.ConnectionStringEnvironmentVariable)))
        {
            Skip = $"Set {PostgreSqlTestDatabase.ConnectionStringEnvironmentVariable} to run PostgreSQL integration tests.";
        }
    }
}
