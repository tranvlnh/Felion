using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Felion.Bot;

/// <summary>
/// Registers the Discord transport foundation.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the configured single-guild Discord Gateway integration.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="healthChecks">The health check builder.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFelionBot(
        this IServiceCollection services,
        IConfiguration configuration,
        IHealthChecksBuilder healthChecks)
    {
        var token = configuration["Discord:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return services;
        }

        var guildId = configuration["Discord:GuildId"];
        if (!ulong.TryParse(guildId, out var parsedGuildId) || parsedGuildId == 0)
        {
            throw new InvalidOperationException(
                "Discord:GuildId must be a positive snowflake when Discord:Token is configured.");
        }

        services.AddDiscordGateway(options =>
        {
            options.Token = token;
            options.Intents = GatewayIntents.Guilds;
        });
        healthChecks.AddCheck<DiscordGatewayHealthCheck>("discord-gateway", tags: ["ready"]);

        return services;
    }
}
