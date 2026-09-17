using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed record RelinkDiscordCommand(long DiscordUserId);

public sealed record DiscordUnlinkResult(
    Guid MemberId,
    long DiscordUserId,
    bool RolesSynchronized);

public sealed record DiscordRelinkResult(
    Guid MemberId,
    long PreviousDiscordUserId,
    long DiscordUserId,
    bool RolesSynchronized);

public sealed record DiscordForceSyncResult(
    Guid MemberId,
    long DiscordUserId,
    bool RolesSynchronized);

public interface IDiscordMemberSyncStore
{
    public Task<long?> FindDiscordUserIdAsync(
        Guid memberId,
        CancellationToken cancellationToken);

    public Task UpdateMemberAsync(
        Member member,
        AuditLog auditLog,
        CancellationToken cancellationToken);
}

public interface IDiscordLinkManagementStore
{
    public Task<DiscordIdentityLink?> FindByMemberIdAsync(
        Guid memberId,
        bool track,
        CancellationToken cancellationToken);

    public Task<DiscordIdentityLink?> FindByDiscordUserIdAsync(
        long discordUserId,
        CancellationToken cancellationToken);

    public Task UnlinkAsync(
        DiscordIdentityLink link,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task RelinkAsync(
        DiscordIdentityLink link,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task RecordAuditAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken);
}

public interface IDiscordLinkManagementService
{
    public Task<DiscordUnlinkResult> UnlinkAsync(
        Guid actorMemberId,
        Guid memberId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<DiscordRelinkResult> RelinkAsync(
        Guid actorMemberId,
        Guid memberId,
        RelinkDiscordCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<DiscordForceSyncResult> ForceSyncAsync(
        Guid actorMemberId,
        Guid memberId,
        string correlationId,
        CancellationToken cancellationToken);
}
