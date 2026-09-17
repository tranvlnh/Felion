using System.Net;
using Felion.Application.Discord;
using NetCord;
using NetCord.Rest;

namespace Felion.Bot;

public sealed class NetCordDiscordGuildPermissionGateway(
    RestClient restClient,
    ConfiguredDiscordGuild configuredGuild) : IDiscordGuildPermissionGateway
{
    public async Task<bool> IsAdministratorAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        if (discordUserId <= 0)
        {
            return false;
        }

        try
        {
            var guild = await restClient.GetGuildAsync(
                configuredGuild.Id,
                cancellationToken: cancellationToken);
            if (guild.OwnerId == (ulong)discordUserId)
            {
                return true;
            }
        }
        catch (RestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (RestException)
        {
            throw new DiscordGuildPermissionGatewayException(
                "Discord rejected the guild ownership lookup request.");
        }

        GuildUser guildUser;
        try
        {
            guildUser = await restClient.GetGuildUserAsync(
                configuredGuild.Id,
                (ulong)discordUserId,
                cancellationToken: cancellationToken);
        }
        catch (RestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (RestException)
        {
            throw new DiscordGuildPermissionGatewayException(
                "Discord rejected the guild member permission lookup request.");
        }

        IReadOnlyList<Role> roles;
        try
        {
            roles = await restClient.GetGuildRolesAsync(
                configuredGuild.Id,
                cancellationToken: cancellationToken);
        }
        catch (RestException)
        {
            throw new DiscordGuildPermissionGatewayException(
                "Discord rejected the guild role permission lookup request.");
        }

        var roleIds = guildUser.RoleIds.ToHashSet();
        var permissions = roles
            .Where(role => role.Id == configuredGuild.Id || roleIds.Contains(role.Id))
            .Select(role => role.Permissions)
            .Aggregate(default(Permissions), (current, rolePermissions) => current | rolePermissions);

        return permissions.HasFlag(Permissions.Administrator);
    }
}
