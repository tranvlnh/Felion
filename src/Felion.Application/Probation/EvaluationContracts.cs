using Felion.Domain.Audit;
using Felion.Domain.Evaluation;

namespace Felion.Application.Probation;

public sealed record CreateEvaluationPeriodCommand(string Name);

public sealed record SubmitPeerEvaluationCommand(
    Guid PeriodId,
    Guid TargetCandidateId,
    int Contribution,
    int Communication,
    int Attitude,
    string? Note = null);

public sealed record SubmitMentorEvaluationCommand(
    Guid PeriodId,
    Guid TargetCandidateId,
    int Attendance,
    int TaskCompletion,
    int LearningInitiative,
    string? Note = null);

public sealed record EvaluationPeriodDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    EvaluationPeriodStatus Status);

public sealed record EvaluationTargetDto(
    Guid CandidateId,
    string StudentId,
    string FullName,
    bool HasSubmission);

public sealed record EvaluationTeamTargetDto(
    Guid TeamId,
    string TeamName,
    IReadOnlyList<EvaluationTargetDto> Targets);

public sealed record EvaluationTargetListDto(
    EvaluationPeriodDto Period,
    IReadOnlyList<EvaluationTeamTargetDto> Teams);

public sealed record EvaluationProgressDto(
    int PeerSubmitted,
    int PeerExpected,
    int MentorSubmitted,
    int MentorExpected,
    IReadOnlyList<EvaluationTeamProgressDto> Teams,
    IReadOnlyList<MissingPeerEvaluationDto> MissingPeerEvaluations,
    IReadOnlyList<MissingMentorEvaluationDto> MissingMentorEvaluations);

public sealed record EvaluationTeamProgressDto(
    Guid TeamId,
    string TeamName,
    int PeerSubmitted,
    int PeerExpected,
    int MentorSubmitted,
    int MentorExpected);

public sealed record MissingPeerEvaluationDto(
    string EvaluatorStudentId,
    string EvaluatorName,
    string TargetStudentId,
    string TargetName,
    Guid TeamId,
    string TeamName);

public sealed record MissingMentorEvaluationDto(
    string MentorStudentId,
    string MentorName,
    string TargetStudentId,
    string TargetName,
    Guid TeamId,
    string TeamName);

public sealed record EvaluationStatusDto(
    EvaluationPeriodDto Period,
    EvaluationProgressDto Progress);

public sealed record PeerEvaluationAggregateDto(
    decimal? AverageContribution,
    decimal? AverageCommunication,
    decimal? AverageAttitude,
    int EvaluationCount);

public sealed record MentorEvaluationAggregateDto(
    decimal? AverageAttendance,
    decimal? AverageTaskCompletion,
    decimal? AverageLearningInitiative,
    int EvaluationCount);

public sealed record EvaluationCandidateSummaryDto(
    Guid CandidateId,
    string StudentId,
    string FullName,
    Guid TeamId,
    string TeamName,
    PeerEvaluationAggregateDto Peer,
    MentorEvaluationAggregateDto Mentor);

public sealed record EvaluationSummaryDto(
    EvaluationPeriodDto Period,
    string? TeamName,
    IReadOnlyList<EvaluationCandidateSummaryDto> Candidates,
    EvaluationProgressDto Progress);

public sealed record PeerEvaluationSubmissionDto(
    Guid Id,
    Guid EvaluatorCandidateId,
    string EvaluatorStudentId,
    string EvaluatorName,
    Guid TargetCandidateId,
    string TargetStudentId,
    string TargetName,
    int Contribution,
    int Communication,
    int Attitude,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record MentorEvaluationSubmissionDto(
    Guid Id,
    Guid MentorMemberId,
    string MentorStudentId,
    string MentorName,
    Guid TargetCandidateId,
    string TargetStudentId,
    string TargetName,
    int Attendance,
    int TaskCompletion,
    int LearningInitiative,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record EvaluationDetailDto(
    EvaluationPeriodDto Period,
    EvaluationCandidateSummaryDto Candidate,
    IReadOnlyList<PeerEvaluationSubmissionDto> PeerSubmissions,
    IReadOnlyList<MentorEvaluationSubmissionDto> MentorSubmissions);

public sealed record EvaluationSubmissionReceiptDto(
    Guid Id,
    bool Updated,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public interface IEvaluationStore
{
    public Task<IReadOnlyList<EvaluationPeriod>> ListPeriodsAsync(CancellationToken cancellationToken);

    public Task<EvaluationPeriod?> FindPeriodAsync(
        Guid periodId,
        bool track,
        CancellationToken cancellationToken);

    public Task<PeerEvaluation?> FindPeerEvaluationAsync(
        Guid periodId,
        Guid evaluatorCandidateId,
        Guid targetCandidateId,
        bool track,
        CancellationToken cancellationToken);

    public Task<MentorEvaluation?> FindMentorEvaluationAsync(
        Guid periodId,
        Guid mentorMemberId,
        Guid targetCandidateId,
        bool track,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<PeerEvaluation>> ListPeerEvaluationsAsync(
        Guid periodId,
        Guid? targetCandidateId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<MentorEvaluation>> ListMentorEvaluationsAsync(
        Guid periodId,
        Guid? targetCandidateId,
        CancellationToken cancellationToken);

    public Task AddPeriodAsync(
        EvaluationPeriod period,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task SavePeerEvaluationAsync(
        PeerEvaluation evaluation,
        AuditLog auditLog,
        bool isNew,
        CancellationToken cancellationToken);

    public Task SaveMentorEvaluationAsync(
        MentorEvaluation evaluation,
        AuditLog auditLog,
        bool isNew,
        CancellationToken cancellationToken);

    public Task ClosePeriodAsync(
        EvaluationPeriod period,
        AuditLog auditLog,
        CancellationToken cancellationToken);
}

public interface IEvaluationManagementService
{
    public Task<EvaluationPeriodDto> CreatePeriodAsync(
        Guid actorMemberId,
        CreateEvaluationPeriodCommand command,
        string correlationId,
        long? actorDiscordUserId,
        CancellationToken cancellationToken);

    public Task<EvaluationPeriodDto> ClosePeriodAsync(
        Guid actorMemberId,
        Guid periodId,
        string correlationId,
        long? actorDiscordUserId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<EvaluationPeriodDto>> ListPeriodsAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken);

    public Task<EvaluationPeriodDto?> GetCurrentPeriodAsync(CancellationToken cancellationToken);

    public Task<EvaluationStatusDto?> GetCurrentStatusAsync(CancellationToken cancellationToken);

    public Task<EvaluationStatusDto> GetStatusAsync(
        Guid actorMemberId,
        Guid? periodId,
        CancellationToken cancellationToken);

    public Task<EvaluationStatusDto> GetStatusByNameAsync(
        Guid actorMemberId,
        string periodName,
        CancellationToken cancellationToken);

    public Task<EvaluationTargetListDto> GetPeerTargetsAsync(
        long discordUserId,
        Guid? periodId,
        CancellationToken cancellationToken);

    public Task<EvaluationTargetListDto> GetMentorTargetsAsync(
        long discordUserId,
        Guid? periodId,
        CancellationToken cancellationToken);

    public Task<PeerEvaluationSubmissionDto?> GetPeerEvaluationAsync(
        long discordUserId,
        Guid periodId,
        Guid targetCandidateId,
        CancellationToken cancellationToken);

    public Task<MentorEvaluationSubmissionDto?> GetMentorEvaluationAsync(
        long discordUserId,
        Guid periodId,
        Guid targetCandidateId,
        CancellationToken cancellationToken);

    public Task<EvaluationSubmissionReceiptDto> SubmitPeerEvaluationAsync(
        long discordUserId,
        SubmitPeerEvaluationCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EvaluationSubmissionReceiptDto> SubmitMentorEvaluationAsync(
        long discordUserId,
        SubmitMentorEvaluationCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EvaluationDetailDto> ViewAsync(
        Guid actorMemberId,
        Guid candidateId,
        Guid? periodId,
        CancellationToken cancellationToken);

    public Task<EvaluationDetailDto> ViewByPeriodNameAsync(
        Guid actorMemberId,
        Guid candidateId,
        string? periodName,
        CancellationToken cancellationToken);

    public Task<EvaluationSummaryDto> SummaryAsync(
        Guid actorMemberId,
        Guid periodId,
        Guid? teamId,
        CancellationToken cancellationToken);

    public Task<EvaluationSummaryDto> SummaryByNameAsync(
        Guid actorMemberId,
        string periodName,
        string? teamName,
        CancellationToken cancellationToken);
}
