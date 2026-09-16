using Felion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Felion.Infrastructure;

/// <summary>
/// Registers Felion infrastructure adapters.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers PostgreSQL persistence when a connection string is configured.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="healthChecks">The health check builder.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFelionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHealthChecksBuilder healthChecks)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        services.AddDbContext<FelionDbContext>(options => options.UseNpgsql(connectionString));
        healthChecks.AddDbContextCheck<FelionDbContext>("postgres", tags: ["ready"]);

        return services;
    }
}
