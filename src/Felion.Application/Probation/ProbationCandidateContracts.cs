using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Probation;

namespace Felion.Application.Probation;

public sealed record CreateProbationCandidateCommand(
    string StudentId,
    string FullName,
    Guid DepartmentId,
    Guid GenerationId);

public sealed record UpdateProbationCandidateCommand(
    string StudentId,
    string FullName,
    Guid DepartmentId,
    Guid GenerationId);

public sealed record ListProbationCandidatesQuery(
    int Page = 1,
    int PageSize = 50,
    string? Search = null,
    Guid? DepartmentId = null,
    Guid? GenerationId = null,
    Guid? TeamId = null,
    bool? HasTeam = null,
    ProbationCandidateStatus? Status = null);

public sealed record ProbationCandidateReference(
    Guid Id,
    string Name,
    string? Code = null);

public sealed record ProbationCandidateTeamReference(Guid Id, string Name, bool IsActive);

public sealed record ProbationCandidateDto(
    Guid Id,
    string StudentId,
    string FullName,
    ProbationCandidateReference Department,
    ProbationCandidateReference Generation,
    ProbationCandidateTeamReference? Team,
    ProbationCandidateStatus Status,
    bool HasDiscordIdentity,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProbationCandidatePage(
    IReadOnlyList<ProbationCandidateDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ProbationManagementReferenceData(
    IReadOnlyList<ProbationCandidateReference> Departments,
    IReadOnlyList<ProbationCandidateReference> Generations,
    IReadOnlyList<ProbationCandidateTeamReference> Teams);

public sealed record ProbationMentorOption(
    Guid MemberId,
    string StudentId,
    string FullName);

public sealed record ProbationCandidateView(
    ProbationCandidate Candidate,
    ProbationCandidateReference Department,
    ProbationCandidateReference Generation,
    ProbationCandidateTeamReference? Team,
    bool HasDiscordIdentity);

public interface IProbationCandidateStore
{
    public Task<(IReadOnlyList<ProbationCandidateView> Items, int TotalCount)> ListAsync(
        ListProbationCandidatesQuery query,
        CancellationToken cancellationToken);

    public Task<ProbationCandidateView?> FindViewAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    public Task<ProbationCandidate?> FindCandidateAsync(
        Guid candidateId,
        bool track,
        CancellationToken cancellationToken);

    public Task<DiscordIdentityLink?> FindIdentityLinkAsync(
        Guid candidateId,
        bool track,
        CancellationToken cancellationToken);

    public Task<bool> IsStudentIdTakenAsync(
        string studentId,
        Guid? excludedCandidateId,
        CancellationToken cancellationToken);

    public Task<ProbationTeam?> FindTeamAsync(
        Guid teamId,
        bool track,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<ProbationCandidateTeamReference>> ListTeamsAsync(
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<ProbationMentorOption>> SearchActiveMentorsAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken);

    public Task AddAsync(
        ProbationCandidate candidate,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task SaveUpdateAsync(
        ProbationCandidate candidate,
        DiscordIdentityLink? identityLink,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task SaveTeamChangeAsync(
        ProbationCandidate candidate,
        AuditLog auditLog,
        DiscordSyncJob? syncJob,
        CancellationToken cancellationToken);
}

public interface IProbationCandidateManagementService
{
    public Task<ProbationCandidatePage> ListAsync(
        Guid actorMemberId,
        ListProbationCandidatesQuery query,
        CancellationToken cancellationToken);

    public Task<ProbationCandidateDto> GetAsync(
        Guid actorMemberId,
        Guid candidateId,
        CancellationToken cancellationToken);

    public Task<ProbationCandidateDto> CreateAsync(
        Guid actorMemberId,
        CreateProbationCandidateCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<ProbationCandidateDto> UpdateAsync(
        Guid actorMemberId,
        Guid candidateId,
        UpdateProbationCandidateCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<ProbationCandidateDto> ChangeTeamAsync(
        Guid actorMemberId,
        Guid candidateId,
        Guid? teamId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<ProbationManagementReferenceData> GetReferenceDataAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<ProbationMentorOption>> SearchMentorsAsync(
        Guid actorMemberId,
        string? search,
        int limit,
        CancellationToken cancellationToken);
}
