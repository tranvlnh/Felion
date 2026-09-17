using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Discord;

public sealed record DiscordRoleAssignmentDto(
    string DiscordRoleId,
    string RoleNameSnapshot);

public sealed record DiscordRoleAssignmentSubjectView(
    Guid SubjectId,
    DiscordIdentitySubjectType SubjectType,
    string StudentId,
    string FullName,
    MemberPosition? MemberPosition,
    MemberStatus? MemberStatus,
    ProbationCandidateStatus? CandidateStatus,
    long? DiscordUserId,
    IReadOnlyList<DiscordRoleAssignment> Assignments);

public sealed record DiscordRoleAssignmentSubjectDto(
    Guid SubjectId,
    DiscordIdentitySubjectType SubjectType,
    string StudentId,
    string FullName,
    MemberPosition? MemberPosition,
    MemberStatus? MemberStatus,
    ProbationCandidateStatus? CandidateStatus,
    long? DiscordUserId,
    IReadOnlyList<DiscordRoleAssignmentDto> Assignments);

public sealed record DiscordRoleAssignmentDashboard(
    IReadOnlyList<DiscordRoleAssignmentSubjectDto> Subjects);

public sealed record DiscordAssignableRoleDto(
    string Id,
    string Name,
    int RawPosition);

public sealed record UpdateDiscordRoleAssignmentsCommand(
    IReadOnlyCollection<long> DiscordRoleIds);

public interface IDiscordRoleAssignmentStore
{
    public Task<IReadOnlyList<DiscordRoleAssignmentSubjectView>> ListSubjectsAsync(
        CancellationToken cancellationToken);

    public Task<DiscordRoleAssignmentSubjectView?> FindSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken);

    public Task ReplaceAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        IReadOnlyCollection<long> expectedRoleIds,
        IReadOnlyCollection<DiscordRoleAssignment> desiredAssignments,
        AuditLog auditLog,
        DiscordSyncJob? syncJob,
        CancellationToken cancellationToken);
}

public interface IDiscordRoleAssignmentService
{
    public Task<DiscordRoleAssignmentDashboard> ListAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<DiscordAssignableRoleDto>> ListRolesAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken);

    public Task<DiscordRoleAssignmentSubjectDto> ReplaceAsync(
        Guid actorMemberId,
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        UpdateDiscordRoleAssignmentsCommand command,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class DiscordRoleAssignmentAccessDeniedException()
    : Exception("Only an active Admin member may manage individual Discord role assignments.");

public sealed class DiscordRoleAssignmentNotFoundException()
    : Exception("The active Discord role assignment subject was not found.");

public sealed class DiscordRoleAssignmentValidationException(string message) : Exception(message);

public sealed class DiscordRoleAssignmentConflictException(string message) : Exception(message);
