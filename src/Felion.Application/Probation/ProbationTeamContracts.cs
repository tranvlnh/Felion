using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Probation;

namespace Felion.Application.Probation;

public sealed record CreateProbationTeamCommand(string Name);

public sealed record UpdateProbationTeamCommand(string? Name = null, bool? IsActive = null);

public sealed record ProbationTeamDto(
    Guid Id,
    string Name,
    bool IsActive,
    IReadOnlyList<Guid> CandidateIds,
    IReadOnlyList<Guid> MentorMemberIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public IReadOnlyList<ProbationTeamCandidateSummary> Candidates { get; init; } = [];

    public IReadOnlyList<ProbationTeamMentorSummary> Mentors { get; init; } = [];
}

public sealed record ProbationTeamCandidateSummary(
    Guid Id,
    string StudentId,
    string FullName);

public sealed record ProbationTeamMentorSummary(
    Guid MemberId,
    string StudentId,
    string FullName);

public sealed record ProbationTeamView(
    Guid Id,
    string Name,
    bool IsActive,
    IReadOnlyList<Guid> CandidateIds,
    IReadOnlyList<Guid> MentorMemberIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ProbationTeamCandidateSummary>? Candidates = null,
    IReadOnlyList<ProbationTeamMentorSummary>? Mentors = null);

public interface IProbationTeamStore
{
    public Task<IReadOnlyList<ProbationTeamView>> ListAsync(CancellationToken cancellationToken);

    public Task<ProbationTeamView?> FindViewAsync(Guid teamId, CancellationToken cancellationToken);

    public Task<ProbationTeam?> FindTeamAsync(
        Guid teamId,
        bool track,
        CancellationToken cancellationToken);

    public Task<ProbationCandidate?> FindCandidateAsync(
        Guid candidateId,
        bool track,
        CancellationToken cancellationToken);

    public Task<TeamMentor?> FindMentorAsync(
        Guid teamId,
        Guid memberId,
        bool track,
        CancellationToken cancellationToken);

    public Task<long?> FindCandidateDiscordUserIdAsync(
        Guid candidateId,
        CancellationToken cancellationToken);

    public Task AddTeamAsync(
        ProbationTeam team,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task UpdateTeamAsync(
        ProbationTeam team,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task SaveCandidateAssignmentAsync(
        ProbationCandidate candidate,
        AuditLog auditLog,
        DiscordSyncJob? syncJob,
        CancellationToken cancellationToken);

    public Task AddMentorAsync(
        TeamMentor mentor,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task RemoveMentorAsync(
        TeamMentor mentor,
        AuditLog auditLog,
        CancellationToken cancellationToken);
}

public interface IProbationTeamManagementService
{
    public Task<IReadOnlyList<ProbationTeamDto>> ListAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken);

    public Task<ProbationTeamDto> GetAsync(
        Guid actorMemberId,
        Guid teamId,
        CancellationToken cancellationToken);

    public Task<ProbationTeamDto> CreateAsync(
        Guid actorMemberId,
        CreateProbationTeamCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<ProbationTeamDto> UpdateAsync(
        Guid actorMemberId,
        Guid teamId,
        UpdateProbationTeamCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<ProbationTeamDto> AssignCandidateAsync(
        Guid actorMemberId,
        Guid teamId,
        Guid candidateId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<ProbationTeamDto> RemoveCandidateAsync(
        Guid actorMemberId,
        Guid teamId,
        Guid candidateId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<ProbationTeamDto> AssignMentorAsync(
        Guid actorMemberId,
        Guid teamId,
        Guid mentorMemberId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<ProbationTeamDto> RemoveMentorAsync(
        Guid actorMemberId,
        Guid teamId,
        Guid mentorMemberId,
        string correlationId,
        CancellationToken cancellationToken);
}
