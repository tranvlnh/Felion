using System.Net;
using Felion.Application.Discord;
using Felion.Bot.Configuration;
using NetCord;
using NetCord.Rest;

namespace Felion.Bot.Gateways;

public sealed class NetCordDiscordRoleGateway(
    RestClient restClient,
    ConfiguredDiscordGuild configuredGuild) : IDiscordRoleGateway, IDiscordGuildRoleCatalog
{
    public async Task<IReadOnlyList<DiscordGuildRoleSnapshot>> ListRolesAsync(
        CancellationToken cancellationToken)
    {
        var roles = await GetRolesAsync(cancellationToken);
        var botGuildUser = await GetCurrentGuildUserAsync(cancellationToken);
        var botPermissions = GetEffectivePermissions(roles, botGuildUser);
        var botHighestRolePosition = GetHighestRolePosition(roles, botGuildUser);

        return roles
            .Select(role => ToGuildRoleSnapshot(
                role,
                CanAssignRole(role, botPermissions, botHighestRolePosition)))
            .ToArray();
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

        var managedIds = managedRoleIds.Select(ToSnowflake).ToHashSet();
        var desiredIds = desiredRoleIds.Select(ToSnowflake).ToHashSet();
        if (!desiredIds.IsSubsetOf(managedIds))
        {
            throw new DiscordRoleGatewayException(
                "The desired Discord roles must be a subset of the managed role mappings.");
        }

        if (managedIds.Count == 0)
        {
            return;
        }

        EnsureRolePermission(roles, botGuildUser);
        var botPermissions = GetEffectivePermissions(roles, botGuildUser);

        var managedRoles = roles
            .Where(role => managedIds.Contains(role.Id))
            .ToDictionary(role => role.Id);
        if (managedRoles.Count != managedIds.Count)
        {
            throw new DiscordRoleNotFoundException(
                "At least one configured Discord role does not exist in the configured guild.");
        }

        var botHighestRolePosition = GetHighestRolePosition(roles, botGuildUser);
        var manageableRoleIds = managedRoles.Values
            .Where(role => role.Id != configuredGuild.Id
                && CanAssignRole(role, botPermissions, botHighestRolePosition))
            .Select(role => role.Id)
            .ToHashSet();
        var unmanageableDesiredRole = desiredIds
            .Except(manageableRoleIds)
            .FirstOrDefault();
        if (unmanageableDesiredRole != 0)
        {
            var role = managedRoles[unmanageableDesiredRole];
            throw new DiscordRolePermissionException(
                $"The bot cannot manage Discord role '{role.Name}' at position {role.RawPosition} because of role hierarchy or role ownership.");
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
                         .Intersect(manageableRoleIds)
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
            var currentUser = await restClient.GetCurrentUserAsync(
                cancellationToken: cancellationToken);
            return await restClient.GetGuildUserAsync(
                configuredGuild.Id,
                currentUser.Id,
                cancellationToken: cancellationToken);
        }
        catch (RestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new DiscordRoleNotFoundException(
                "The bot is not a member of the configured Discord guild, or Discord:GuildId is incorrect.");
        }
        catch (RestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new DiscordRolePermissionException(
                "Discord denied access to the bot member in the configured guild.");
        }
        catch (RestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new DiscordRoleGatewayException(
                "Discord rejected the bot token while reading the bot member.");
        }
        catch (RestException exception)
        {
            throw new DiscordRoleGatewayException(
                $"Discord rejected the bot member lookup request (HTTP {(int)exception.StatusCode}).");
        }
    }

    private static Permissions GetEffectivePermissions(
        IReadOnlyList<Role> roles,
        GuildUser botGuildUser)
    {
        var botRoleIds = botGuildUser.RoleIds.ToHashSet();
        return roles
            .Where(role => role.Id == botGuildUser.GuildId || botRoleIds.Contains(role.Id))
            .Select(role => role.Permissions)
            .Aggregate(default(Permissions), (current, rolePermissions) => current | rolePermissions);
    }

    private static int GetHighestRolePosition(
        IReadOnlyList<Role> roles,
        GuildUser botGuildUser)
    {
        var botRoleIds = botGuildUser.RoleIds.ToHashSet();
        return roles
            .Where(role => botRoleIds.Contains(role.Id))
            .Select(role => role.RawPosition)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static bool CanAssignRole(
        Role role,
        Permissions botPermissions,
        int botHighestRolePosition)
    {
        return !role.Managed
            && role.RawPosition < botHighestRolePosition
            && (botPermissions.HasFlag(Permissions.Administrator)
                || botPermissions.HasFlag(Permissions.ManageRoles));
    }

    private static void EnsureRolePermission(
        IReadOnlyList<Role> roles,
        GuildUser botGuildUser)
    {
        var permissions = GetEffectivePermissions(roles, botGuildUser);

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

    private DiscordGuildRoleSnapshot ToGuildRoleSnapshot(Role role, bool isAssignableByBot)
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
            role.RawPosition,
            isAssignableByBot);
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
