using Felion.Application.Discord;
using Felion.Application.Identity;
using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Infrastructure.MemberImports;
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
        services.AddScoped<MemberStore>();
        services.AddScoped<IMemberStore>(services => services.GetRequiredService<MemberStore>());
        services.AddScoped<IWebIdentityDirectory>(services => services.GetRequiredService<MemberStore>());
        services.AddScoped<IWebIdentityService, WebIdentityService>();
        services.AddScoped<IMemberManagementService, MemberManagementService>();
        services.AddScoped<IProbationTeamStore, ProbationTeamStore>();
        services.AddScoped<IProbationTeamManagementService, ProbationTeamManagementService>();
        services.AddScoped<IDiscordLinkStore, DiscordLinkStore>();
        services.AddScoped<IDiscordLinkingService, DiscordLinkingService>();
        services.AddScoped<IDiscordLinkManagementStore, DiscordLinkManagementStore>();
        services.AddScoped<IDiscordLinkManagementService, DiscordLinkManagementService>();
        services.AddScoped<IDiscordRoleMappingStore, DiscordRoleMappingStore>();
        services.AddScoped<IDiscordRoleMappingService, DiscordRoleMappingService>();
        services.AddScoped<IDiscordRoleManagementStore, DiscordRoleManagementStore>();
        services.AddScoped<IDiscordRoleManagementService, DiscordRoleManagementService>();
        services.AddScoped<IDiscordSyncJobStore, DiscordSyncJobStore>();
        services.AddScoped<IDiscordSyncProcessor, DiscordSyncProcessor>();
        services.AddSingleton<IMemberImportReader, MemberImportReader>();

        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        services.AddDbContext<FelionDbContext>(options =>
            options
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsAssembly(typeof(FelionDbContext).Assembly.FullName)));
        healthChecks.AddDbContextCheck<FelionDbContext>("postgres", tags: ["ready"]);

        return services;
    }
}
