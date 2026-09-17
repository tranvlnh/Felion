using Felion.Domain.Audit;
using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public sealed record DiscordRoleSyncFailure(
    Guid SubjectId,
    DiscordIdentitySubjectType SubjectType,
    long DiscordUserId,
    string Error);

public sealed record DiscordRoleSyncAllResult(
    int TotalSubjects,
    int SynchronizedSubjects,
    IReadOnlyList<DiscordRoleSyncFailure> Failures);

public interface IDiscordRoleSynchronizationStore
{
    public Task<IReadOnlyList<DiscordRoleMapping>> ListRoleMappingsAsync(
        CancellationToken cancellationToken);

    public Task RecordAuditAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<DiscordRoleSyncTarget>> ListLinkedActiveTargetsAsync(
        CancellationToken cancellationToken);

    public Task<DiscordRoleSyncTarget?> FindTargetAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken);

    public Task<long?> FindDiscordUserIdAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<DiscordRoleAssignment>> ListRoleAssignmentsAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken);
}

public interface IDiscordRoleSynchronizationService
{
    public Task SynchronizeSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken);

    public Task SynchronizeSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        IReadOnlyCollection<long> additionalManagedRoleIds,
        CancellationToken cancellationToken);

    public Task ClearSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        long discordUserId,
        CancellationToken cancellationToken);

    public Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        CancellationToken cancellationToken);

    public Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        IReadOnlyCollection<long> additionalManagedRoleIds,
        CancellationToken cancellationToken);

    public Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        Guid actorMemberId,
        long actorDiscordUserId,
        string correlationId,
        CancellationToken cancellationToken);
}
