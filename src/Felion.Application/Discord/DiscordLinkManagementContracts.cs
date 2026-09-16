using Felion.Domain.Audit;
using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public sealed record RelinkDiscordCommand(long DiscordUserId);

public sealed record DiscordUnlinkResult(
    Guid MemberId,
    long DiscordUserId,
    bool SyncQueued);

public sealed record DiscordRelinkResult(
    Guid MemberId,
    long PreviousDiscordUserId,
    long DiscordUserId,
    bool SyncQueued);

public sealed record DiscordForceSyncResult(
    Guid MemberId,
    long DiscordUserId,
    bool SyncQueued);

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
        DiscordSyncJob clearRolesJob,
        CancellationToken cancellationToken);

    public Task RelinkAsync(
        DiscordIdentityLink link,
        AuditLog auditLog,
        IReadOnlyCollection<DiscordSyncJob> syncJobs,
        CancellationToken cancellationToken);

    public Task EnqueueSyncAsync(
        AuditLog auditLog,
        DiscordSyncJob syncJob,
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
