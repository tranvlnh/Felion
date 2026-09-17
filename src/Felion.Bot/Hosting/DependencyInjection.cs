using Felion.Application.Discord;
using Felion.Bot.Components.Evaluation;
using Felion.Bot.Components.Teams;
using Felion.Bot.Components.Verification;
using Felion.Bot.Configuration;
using Felion.Bot.Gateways;
using Felion.Bot.HealthChecks;
using Felion.Bot.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace Felion.Bot.Hosting;

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

        services.AddSingleton(new ConfiguredDiscordGuild(parsedGuildId));
        services.AddSingleton<IDiscordRoleGateway, NetCordDiscordRoleGateway>();
        services.AddScoped<IDiscordRoleSynchronizationService, DiscordRoleSynchronizationService>();
        services.AddSingleton<IDiscordGuildRoleCatalog>(services =>
            (IDiscordGuildRoleCatalog)services.GetRequiredService<IDiscordRoleGateway>());
        services.AddSingleton<IDiscordGuildPermissionGateway, NetCordDiscordGuildPermissionGateway>();
        services.AddSingleton<IDiscordVerificationMessageGateway, NetCordDiscordVerificationMessageGateway>();
        services.AddSingleton<IDiscordGuildGateway, NetCordDiscordGuildGateway>();
        services.AddHostedService<DiscordSyncWorker>();
        services.AddHostedService<DiscordCommandRegistrationWorker>();
        services
            .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
            .AddComponentInteractions<ModalInteraction, ModalInteractionContext>()
            .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>()
            .AddComponentInteractions<RoleMenuInteraction, RoleMenuInteractionContext>();
        services.AddApplicationCommands(options =>
        {
            options.AutoRegisterCommands = false;
            options.DefaultContexts = [InteractionContextType.Guild];
        });

        services.AddDiscordGateway(options =>
        {
            options.Token = token;
            options.Intents = GatewayIntents.Guilds;
        });
        healthChecks.AddCheck<DiscordGatewayHealthCheck>("discord-gateway", tags: ["ready"]);

        return services;
    }

    public static IHost AddFelionBotModules(this IHost host)
    {
        host.AddModules(typeof(VerificationButtonModule).Assembly);
        return host;
    }
}
