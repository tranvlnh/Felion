using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed record DiscordRoleSnapshot(long Id, string Name);

public sealed record DiscordGuildRoleSnapshot(
    long Id,
    string Name,
    bool IsManaged,
    bool IsEveryone,
    int RawPosition);

public sealed record DiscordRoleSyncTarget(
    long DiscordUserId,
    DiscordIdentitySubjectType SubjectType,
    MemberPosition? Position,
    Guid DepartmentId,
    Guid GenerationId,
    Guid? ProbationTeamId);

public interface IDiscordRoleGateway
{
    public Task<DiscordRoleSnapshot> GetRoleAsync(
        long discordRoleId,
        CancellationToken cancellationToken);

    public Task<DiscordRoleSnapshot> CreateRoleAsync(
        string name,
        CancellationToken cancellationToken);

    public Task SynchronizeUserRolesAsync(
        long discordUserId,
        IReadOnlyCollection<long> desiredRoleIds,
        IReadOnlyCollection<long> managedRoleIds,
        CancellationToken cancellationToken);
}

public interface IDiscordGuildRoleCatalog
{
    public Task<IReadOnlyList<DiscordGuildRoleSnapshot>> ListRolesAsync(
        CancellationToken cancellationToken);
}

public class DiscordRoleGatewayException(string message) : Exception(message);

public sealed class DiscordRoleNotFoundException(string message) : DiscordRoleGatewayException(message);

public sealed class DiscordRolePermissionException(string message) : DiscordRoleGatewayException(message);

public sealed class DiscordRoleUnavailableException()
    : DiscordRoleGatewayException("Discord role operations are unavailable because the Discord gateway is not configured.");
