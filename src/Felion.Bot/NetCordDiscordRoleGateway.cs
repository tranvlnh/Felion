using System.Net;
using Felion.Application.Discord;
using NetCord;
using NetCord.Rest;

namespace Felion.Bot;

public sealed class NetCordDiscordRoleGateway(
    RestClient restClient,
    ConfiguredDiscordGuild configuredGuild) : IDiscordRoleGateway, IDiscordGuildRoleCatalog
{
    public async Task<IReadOnlyList<DiscordGuildRoleSnapshot>> ListRolesAsync(
        CancellationToken cancellationToken)
    {
        var roles = await GetRolesAsync(cancellationToken);
        return roles.Select(ToGuildRoleSnapshot).ToArray();
    }

    public async Task<DiscordRoleSnapshot> GetRoleAsync(
        long discordRoleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var role = await restClient.GetGuildRoleAsync(
                configuredGuild.Id,
                ToSnowflake(discordRoleId),
                cancellationToken: cancellationToken);
            return ToSnapshot(role);
        }
        catch (RestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new DiscordRoleNotFoundException(
                "The Discord role does not exist in the configured guild.");
        }
        catch (RestException)
        {
            throw new DiscordRoleGatewayException("Discord rejected the role lookup request.");
        }
    }

    public async Task<DiscordRoleSnapshot> CreateRoleAsync(
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            var role = await restClient.CreateGuildRoleAsync(
                configuredGuild.Id,
                new RoleProperties
                {
                    Name = name,
                    Hoist = false,
                    Mentionable = false
                },
                cancellationToken: cancellationToken);
            return ToSnapshot(role);
        }
        catch (RestException)
        {
            throw new DiscordRoleGatewayException("Discord rejected the role creation request.");
        }
    }

    public async Task SynchronizeUserRolesAsync(
        long discordUserId,
        IReadOnlyCollection<long> desiredRoleIds,
        IReadOnlyCollection<long> managedRoleIds,
        CancellationToken cancellationToken)
    {
        var roles = await GetRolesAsync(cancellationToken);
        var botGuildUser = await GetCurrentGuildUserAsync(cancellationToken);
        EnsureRolePermission(roles, botGuildUser);

        var managedIds = managedRoleIds.Select(ToSnowflake).ToHashSet();
        var desiredIds = desiredRoleIds.Select(ToSnowflake).ToHashSet();
        if (!desiredIds.IsSubsetOf(managedIds))
        {
            throw new DiscordRoleGatewayException(
                "The desired Discord roles must be a subset of the managed role mappings.");
        }

        var managedRoles = roles
            .Where(role => managedIds.Contains(role.Id))
            .ToDictionary(role => role.Id);
        if (managedRoles.Count != managedIds.Count)
        {
            throw new DiscordRoleNotFoundException(
                "At least one configured Discord role does not exist in the configured guild.");
        }

        var botHighestRolePosition = roles
            .Where(role => botGuildUser.RoleIds.Contains(role.Id))
            .Select(role => role.RawPosition)
            .DefaultIfEmpty(0)
            .Max();
        foreach (var role in managedRoles.Values)
        {
            if (role.Id == configuredGuild.Id
                || role.Managed
                || role.RawPosition >= botHighestRolePosition)
            {
                throw new DiscordRolePermissionException(
                    "The bot cannot manage one or more configured Discord roles because of role hierarchy or role ownership.");
            }
        }

        GuildUser guildUser;
        try
        {
            guildUser = await restClient.GetGuildUserAsync(
                configuredGuild.Id,
                ToSnowflake(discordUserId),
                cancellationToken: cancellationToken);
        }
        catch (RestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new DiscordRoleNotFoundException(
                "The linked Discord user is not a member of the configured guild.");
        }
        catch (RestException)
        {
            throw new DiscordRoleGatewayException("Discord rejected the guild member lookup request.");
        }

        var currentRoleIds = guildUser.RoleIds.ToHashSet();
        try
        {
            foreach (var roleId in desiredIds.Except(currentRoleIds))
            {
                await restClient.AddGuildUserRoleAsync(
                    configuredGuild.Id,
                    guildUser.Id,
                    roleId,
                    cancellationToken: cancellationToken);
            }

            foreach (var roleId in currentRoleIds
                         .Intersect(managedIds)
                         .Except(desiredIds))
            {
                await restClient.RemoveGuildUserRoleAsync(
                    configuredGuild.Id,
                    guildUser.Id,
                    roleId,
                    cancellationToken: cancellationToken);
            }
        }
        catch (RestException)
        {
            throw new DiscordRoleGatewayException("Discord rejected the guild member role synchronization request.");
        }
    }

    private async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await restClient.GetGuildRolesAsync(
                configuredGuild.Id,
                cancellationToken: cancellationToken);
        }
        catch (RestException)
        {
            throw new DiscordRoleGatewayException("Discord rejected the guild role lookup request.");
        }
    }

    private async Task<GuildUser> GetCurrentGuildUserAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await restClient.GetCurrentUserGuildUserAsync(
                configuredGuild.Id,
                cancellationToken: cancellationToken);
        }
        catch (RestException)
        {
            throw new DiscordRoleGatewayException("Discord rejected the bot member lookup request.");
        }
    }

    private static void EnsureRolePermission(
        IReadOnlyList<Role> roles,
        GuildUser botGuildUser)
    {
        var botRoleIds = botGuildUser.RoleIds.ToHashSet();
        var permissions = roles
            .Where(role => role.Id == botGuildUser.GuildId || botRoleIds.Contains(role.Id))
            .Select(role => role.Permissions)
            .Aggregate(default(Permissions), (current, rolePermissions) => current | rolePermissions);

        if (!permissions.HasFlag(Permissions.Administrator)
            && !permissions.HasFlag(Permissions.ManageRoles))
        {
            throw new DiscordRolePermissionException(
                "The bot requires Manage Roles permission in the configured guild.");
        }
    }

    private static DiscordRoleSnapshot ToSnapshot(Role role)
    {
        if (role.Id > long.MaxValue)
        {
            throw new DiscordRoleGatewayException(
                "The Discord role ID is outside the supported signed bigint range.");
        }

        return new DiscordRoleSnapshot((long)role.Id, role.Name);
    }

    private DiscordGuildRoleSnapshot ToGuildRoleSnapshot(Role role)
    {
        if (role.Id > long.MaxValue)
        {
            throw new DiscordRoleGatewayException(
                "The Discord role ID is outside the supported signed bigint range.");
        }

        return new DiscordGuildRoleSnapshot(
            (long)role.Id,
            role.Name,
            role.Managed,
            role.Id == configuredGuild.Id,
            role.RawPosition);
    }

    private static ulong ToSnowflake(long value)
    {
        if (value <= 0)
        {
            throw new DiscordRoleGatewayException("Discord snowflake IDs must be positive.");
        }

        return (ulong)value;
    }
}
