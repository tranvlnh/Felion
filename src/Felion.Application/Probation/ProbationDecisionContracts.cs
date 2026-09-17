using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Probation;

public enum ProbationDecision
{
    Pass,
    Fail
}

public enum ProbationSuccessPolicy
{
    Archive,
    Delete
}

public enum ProbationFailurePolicy
{
    MarkInactive,
    Delete
}

public sealed record ProbationRetentionPolicy(
    ProbationSuccessPolicy SuccessPolicy = ProbationSuccessPolicy.Archive,
    ProbationFailurePolicy FailurePolicy = ProbationFailurePolicy.MarkInactive);

public interface IProbationRetentionPolicyProvider
{
    public ProbationRetentionPolicy GetPolicy();
}

public interface IWorkspaceEmailGenerator
{
    public string Generate(string fullName);
}

public sealed record ProbationDecisionRequest(
    Guid CandidateId,
    ProbationDecision Decision);

public sealed record ProbationDecisionItemResult(
    Guid CandidateId,
    ProbationDecision Decision,
    bool Succeeded,
    string? Error,
    Guid? MemberId,
    bool DiscordSyncQueued,
    bool KickQueued);

public interface IProbationDecisionStore
{
    public Task<ProbationDecisionCandidateView?> FindCandidateAsync(
        Guid candidateId,
        bool track,
        CancellationToken cancellationToken);

    public Task<bool> MemberStudentIdExistsAsync(
        string studentId,
        CancellationToken cancellationToken);

    public Task<bool> MemberClubEmailExistsAsync(
        string clubEmail,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<DiscordRoleAssignment>> ListRoleAssignmentsAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken);

    public Task SavePassAsync(
        ProbationCandidate candidate,
        Member member,
        DiscordIdentityLink? identityLink,
        DiscordSyncJob? syncJob,
        AuditLog auditLog,
        bool deleteCandidate,
        CancellationToken cancellationToken);

    public Task SaveFailAsync(
        ProbationCandidate candidate,
        DiscordIdentityLink? identityLink,
        DiscordSyncJob? kickJob,
        AuditLog auditLog,
        bool deleteCandidate,
        CancellationToken cancellationToken);
}

public sealed record ProbationDecisionCandidateView(
    ProbationCandidate Candidate,
    DiscordIdentityLink? IdentityLink);

public interface IProbationDecisionService
{
    public Task<IReadOnlyList<ProbationDecisionItemResult>> DecideAsync(
        Guid actorMemberId,
        IReadOnlyCollection<ProbationDecisionRequest> requests,
        string correlationId,
        CancellationToken cancellationToken);
}
