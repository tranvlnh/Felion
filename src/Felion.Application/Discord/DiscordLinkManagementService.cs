using System.Text.Json;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed class DiscordLinkManagementService(
    IDiscordLinkManagementStore store,
    IMemberStore memberStore,
    IDiscordRoleSynchronizationService? roleSynchronizationService = null) : IDiscordLinkManagementService
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

        var audit = CreateAudit(
            actorMemberId,
            "DiscordIdentityUnlinked",
            memberId,
            correlationId,
            before: Snapshot(link));

        await store.UnlinkAsync(link, audit, cancellationToken);
        if (roleSynchronizationService is not null)
        {
            await roleSynchronizationService.ClearSubjectAsync(
                link.SubjectType,
                link.SubjectId,
                link.DiscordUserId,
                cancellationToken);
        }

        return new DiscordUnlinkResult(
            memberId,
            link.DiscordUserId,
            RolesSynchronized: roleSynchronizationService is not null);
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

        var audit = CreateAudit(
            actorMemberId,
            "DiscordIdentityRelinked",
            memberId,
            correlationId,
            before,
            Snapshot(link));

        if (roleSynchronizationService is not null)
        {
            await roleSynchronizationService.ClearSubjectAsync(
                link.SubjectType,
                link.SubjectId,
                previousDiscordUserId,
                cancellationToken);
        }

        await store.RelinkAsync(link, audit, cancellationToken);
        if (roleSynchronizationService is not null)
        {
            await roleSynchronizationService.SynchronizeSubjectAsync(
                link.SubjectType,
                link.SubjectId,
                cancellationToken);
        }
        return new DiscordRelinkResult(
            memberId,
            previousDiscordUserId,
            command.DiscordUserId,
            RolesSynchronized: roleSynchronizationService is not null);
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
        var audit = CreateAudit(
            actorMemberId,
            "DiscordRoleSyncRequested",
            memberId,
            correlationId,
            after: JsonSerializer.Serialize(new
            {
                link.DiscordUserId
            }));

        await store.RecordAuditAsync(audit, cancellationToken);

        if (roleSynchronizationService is not null)
        {
            await roleSynchronizationService.SynchronizeSubjectAsync(
                link.SubjectType,
                link.SubjectId,
                cancellationToken);
        }

        return new DiscordForceSyncResult(
            memberId,
            link.DiscordUserId,
            RolesSynchronized: roleSynchronizationService is not null);
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
