using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Identity;

namespace Felion.Application.Tests;

internal sealed class TestDiscordRoleSynchronizationService : IDiscordRoleSynchronizationService
{
    public List<(DiscordIdentitySubjectType SubjectType, Guid SubjectId)> SynchronizedSubjects { get; } = [];

    public List<(DiscordIdentitySubjectType SubjectType, Guid SubjectId, long DiscordUserId)> ClearedSubjects { get; } = [];

    public int SyncAllCalls { get; private set; }

    public List<AuditLog> Audits { get; } = [];

    public Task RecordAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        Audits.Add(auditLog);
        return Task.CompletedTask;
    }

    public Task SynchronizeSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        SynchronizedSubjects.Add((subjectType, subjectId));
        return Task.CompletedTask;
    }

    public Task SynchronizeSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        IReadOnlyCollection<long> additionalManagedRoleIds,
        CancellationToken cancellationToken)
    {
        SynchronizedSubjects.Add((subjectType, subjectId));
        return Task.CompletedTask;
    }

    public Task ClearSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        long discordUserId,
        CancellationToken cancellationToken)
    {
        ClearedSubjects.Add((subjectType, subjectId, discordUserId));
        return Task.CompletedTask;
    }

    public Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        CancellationToken cancellationToken)
    {
        SyncAllCalls++;
        return Task.FromResult(new DiscordRoleSyncAllResult(
            SynchronizedSubjects.Count,
            SynchronizedSubjects.Count,
            []));
    }

    public Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        IReadOnlyCollection<long> additionalManagedRoleIds,
        CancellationToken cancellationToken)
    {
        return SynchronizeAllAsync(cancellationToken);
    }

    public Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        Guid actorMemberId,
        long actorDiscordUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        return SynchronizeAllAsync(cancellationToken);
    }
}
