using System.Text.Json;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed class DiscordLinkManagementService(
    IDiscordLinkManagementStore store,
    IMemberStore memberStore) : IDiscordLinkManagementService
{
    public async Task<DiscordUnlinkResult> UnlinkAsync(
        Guid actorMemberId,
        Guid memberId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var link = await store.FindByMemberIdAsync(memberId, track: true, cancellationToken)
            ?? throw new DiscordLinkNotFoundException(memberId);

        var clearRolesJob = CreateSyncJob(
            link.SubjectType,
            link.SubjectId,
            DiscordSyncOperation.ClearManagedRoles,
            link.DiscordUserId);
        var audit = CreateAudit(
            actorMemberId,
            "DiscordIdentityUnlinked",
            memberId,
            correlationId,
            before: Snapshot(link));

        await store.UnlinkAsync(link, audit, clearRolesJob, cancellationToken);
        return new DiscordUnlinkResult(memberId, link.DiscordUserId, SyncQueued: true);
    }

    public async Task<DiscordRelinkResult> RelinkAsync(
        Guid actorMemberId,
        Guid memberId,
        RelinkDiscordCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        if (command.DiscordUserId <= 0)
        {
            throw new DiscordLinkValidationException(
                "Discord user ID must be a positive signed snowflake.");
        }

        var link = await store.FindByMemberIdAsync(memberId, track: true, cancellationToken)
            ?? throw new DiscordLinkNotFoundException(memberId);
        var previousDiscordUserId = link.DiscordUserId;
        if (previousDiscordUserId == command.DiscordUserId)
        {
            throw new DiscordLinkValidationException(
                "The new Discord user must differ from the current linked user.");
        }

        var existing = await store.FindByDiscordUserIdAsync(
            command.DiscordUserId,
            cancellationToken);
        if (existing is not null && existing.Id != link.Id)
        {
            throw new DiscordLinkConflictException(
                "The new Discord user is already linked to another identity.");
        }

        var before = Snapshot(link);
        try
        {
            link.Relink(command.DiscordUserId);
        }
        catch (DomainException exception)
        {
            throw new DiscordLinkValidationException(exception.Message);
        }

        var clearRolesJob = CreateSyncJob(
            link.SubjectType,
            link.SubjectId,
            DiscordSyncOperation.ClearManagedRoles,
            previousDiscordUserId);
        var synchronizeRolesJob = CreateSyncJob(
            link.SubjectType,
            link.SubjectId,
            DiscordSyncOperation.SynchronizeRoles,
            command.DiscordUserId);
        var audit = CreateAudit(
            actorMemberId,
            "DiscordIdentityRelinked",
            memberId,
            correlationId,
            before,
            Snapshot(link));

        await store.RelinkAsync(
            link,
            audit,
            [clearRolesJob, synchronizeRolesJob],
            cancellationToken);
        return new DiscordRelinkResult(
            memberId,
            previousDiscordUserId,
            command.DiscordUserId,
            SyncQueued: true);
    }

    public async Task<DiscordForceSyncResult> ForceSyncAsync(
        Guid actorMemberId,
        Guid memberId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var member = await memberStore.FindByIdAsync(memberId, track: false, cancellationToken)
            ?? throw new DiscordLinkNotFoundException(memberId);
        if (member.Status != MemberStatus.Active)
        {
            throw new DiscordLinkValidationException("Only an active Member can be synchronized.");
        }

        var link = await store.FindByMemberIdAsync(memberId, track: false, cancellationToken)
            ?? throw new DiscordLinkNotFoundException(memberId);
        var syncJob = CreateSyncJob(
            link.SubjectType,
            link.SubjectId,
            DiscordSyncOperation.SynchronizeRoles,
            link.DiscordUserId);
        var audit = CreateAudit(
            actorMemberId,
            "DiscordRoleSyncRequested",
            memberId,
            correlationId,
            after: JsonSerializer.Serialize(new
            {
                link.DiscordUserId,
                SyncJobId = syncJob.Id
            }));

        await store.EnqueueSyncAsync(audit, syncJob, cancellationToken);
        return new DiscordForceSyncResult(memberId, link.DiscordUserId, SyncQueued: true);
    }

    private async Task EnsureAuthorizedActorAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position is not (MemberPosition.Admin or MemberPosition.Core))
        {
            throw new DiscordLinkManagementAccessDeniedException();
        }
    }

    private static DiscordSyncJob CreateSyncJob(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        DiscordSyncOperation operation,
        long discordUserId)
    {
        return DiscordSyncJob.Create(
            subjectType,
            subjectId,
            operation,
            JsonSerializer.Serialize(new { DiscordUserId = discordUserId }));
    }

    private static AuditLog CreateAudit(
        Guid actorMemberId,
        string action,
        Guid memberId,
        string correlationId,
        string? before = null,
        string? after = null)
    {
        return AuditLog.Create(
            AuditActorType.WebMember,
            actorMemberId,
            actorDiscordUserId: null,
            action,
            "Member",
            memberId,
            correlationId,
            beforeJson: before,
            afterJson: after);
    }

    private static string Snapshot(DiscordIdentityLink link)
    {
        return JsonSerializer.Serialize(new
        {
            link.Id,
            link.DiscordUserId,
            link.StudentId,
            link.SubjectType,
            link.SubjectId,
            link.LinkedAt
        });
    }
}

public sealed class DiscordLinkManagementAccessDeniedException()
    : Exception("Only an active Core or Admin member may manage Discord identity links.");
