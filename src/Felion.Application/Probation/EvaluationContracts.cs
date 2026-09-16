using Felion.Domain.Audit;
using Felion.Domain.Evaluation;

namespace Felion.Application.Probation;

public sealed record EvaluationDefaults(
    decimal DefaultScoreMin = 1,
    decimal DefaultScoreMax = 5,
    int DefaultTextMaxLength = 2000);

public interface IEvaluationDefaultsProvider
{
    public EvaluationDefaults GetDefaults();
}

public sealed record CreateEvaluationPeriodCommand(
    string Name,
    DateTimeOffset? StartsAt = null,
    DateTimeOffset? EndsAt = null);

public sealed record EvaluationQuestionCommand(
    int Order,
    string Prompt,
    EvaluationQuestionType Type,
    bool IsRequired,
    decimal? ScoreMin = null,
    decimal? ScoreMax = null,
    int? TextMaxLength = null,
    Guid? Id = null);

public sealed record CreateEvaluationFormCommand(
    Guid PeriodId,
    string Name,
    EvaluationReviewerType ReviewerType,
    IReadOnlyList<EvaluationQuestionCommand> Questions);

public sealed record UpdateEvaluationFormCommand(
    string? Name = null,
    bool? IsActive = null,
    IReadOnlyList<EvaluationQuestionCommand>? Questions = null);

public sealed record EvaluationAnswerCommand(
    Guid QuestionId,
    decimal? ScoreValue = null,
    string? TextValue = null);

public sealed record SubmitEvaluationCommand(
    Guid FormId,
    Guid TargetCandidateId,
    IReadOnlyList<EvaluationAnswerCommand> Answers);

public sealed record EvaluationQuestionDto(
    Guid Id,
    Guid FormId,
    int Order,
    string Prompt,
    EvaluationQuestionType Type,
    bool IsRequired,
    decimal? ScoreMin,
    decimal? ScoreMax,
    int? TextMaxLength,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationFormDto(
    Guid Id,
    Guid PeriodId,
    string Name,
    EvaluationReviewerType ReviewerType,
    bool IsActive,
    IReadOnlyList<EvaluationQuestionDto> Questions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationPeriodDto(
    Guid Id,
    string Name,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    EvaluationPeriodStatus Status,
    IReadOnlyList<EvaluationFormDto> Forms,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationQuestionView(
    Guid Id,
    Guid FormId,
    int Order,
    string Prompt,
    EvaluationQuestionType Type,
    bool IsRequired,
    decimal? ScoreMin,
    decimal? ScoreMax,
    int? TextMaxLength,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationFormView(
    Guid Id,
    Guid PeriodId,
    string Name,
    EvaluationReviewerType ReviewerType,
    bool IsActive,
    IReadOnlyList<EvaluationQuestionView> Questions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationPeriodView(
    Guid Id,
    string Name,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    EvaluationPeriodStatus Status,
    IReadOnlyList<EvaluationFormView> Forms,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationAnswerView(
    Guid Id,
    Guid? QuestionId,
    string QuestionPromptSnapshot,
    EvaluationQuestionType QuestionTypeSnapshot,
    decimal? ScoreValue,
    string? TextValue,
    DateTimeOffset CreatedAt);

public sealed record EvaluationSubmissionView(
    Guid Id,
    Guid FormId,
    Guid PeriodId,
    EvaluationReviewerType ReviewerType,
    Guid? ReviewerMemberId,
    Guid? ReviewerCandidateId,
    string ReviewerStudentIdSnapshot,
    string ReviewerNameSnapshot,
    Guid? TargetCandidateId,
    string TargetStudentIdSnapshot,
    string TargetNameSnapshot,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<EvaluationAnswerView> Answers);

public sealed record EvaluationSubmissionReceiptDto(
    Guid Id,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationAnswerDto(
    Guid Id,
    Guid? QuestionId,
    string QuestionPromptSnapshot,
    EvaluationQuestionType QuestionTypeSnapshot,
    decimal? ScoreValue,
    string? TextValue,
    DateTimeOffset CreatedAt);

public sealed record EvaluationSubmissionDto(
    Guid Id,
    Guid FormId,
    Guid PeriodId,
    EvaluationReviewerType ReviewerType,
    Guid? ReviewerMemberId,
    Guid? ReviewerCandidateId,
    string ReviewerStudentIdSnapshot,
    string ReviewerNameSnapshot,
    Guid? TargetCandidateId,
    string TargetStudentIdSnapshot,
    string TargetNameSnapshot,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<EvaluationAnswerDto> Answers);

public interface IEvaluationStore
{
    public Task<IReadOnlyList<EvaluationPeriodView>> ListPeriodsAsync(
        CancellationToken cancellationToken);

    public Task<EvaluationPeriodView?> FindPeriodViewAsync(
        Guid periodId,
        CancellationToken cancellationToken);

    public Task<EvaluationPeriod?> FindPeriodAsync(
        Guid periodId,
        bool track,
        CancellationToken cancellationToken);

    public Task<EvaluationForm?> FindFormAsync(
        Guid formId,
        bool track,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<EvaluationQuestion>> ListQuestionsAsync(
        Guid formId,
        bool track,
        CancellationToken cancellationToken);

    public Task<EvaluationSubmission?> FindSubmissionAsync(
        Guid formId,
        Guid? reviewerMemberId,
        Guid? reviewerCandidateId,
        Guid targetCandidateId,
        bool track,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<EvaluationSubmissionView>> ListSubmissionViewsAsync(
        Guid? periodId,
        Guid? formId,
        CancellationToken cancellationToken);

    public Task AddPeriodAsync(
        EvaluationPeriod period,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task UpdatePeriodAsync(
        EvaluationPeriod period,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task AddFormAsync(
        EvaluationForm form,
        IReadOnlyCollection<EvaluationQuestion> questions,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task UpdateFormAsync(
        EvaluationForm form,
        IReadOnlyCollection<EvaluationQuestion> addedQuestions,
        IReadOnlyCollection<EvaluationQuestion> removedQuestions,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task SaveSubmissionAsync(
        EvaluationSubmission submission,
        IReadOnlyCollection<EvaluationAnswer> answers,
        bool isNew,
        CancellationToken cancellationToken);
}

public interface IEvaluationManagementService
{
    public Task<IReadOnlyList<EvaluationPeriodDto>> ListPeriodsAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken);

    public Task<EvaluationPeriodDto> CreatePeriodAsync(
        Guid actorMemberId,
        CreateEvaluationPeriodCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EvaluationPeriodDto> OpenPeriodAsync(
        Guid actorMemberId,
        Guid periodId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EvaluationPeriodDto> ClosePeriodAsync(
        Guid actorMemberId,
        Guid periodId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EvaluationFormDto> CreateFormAsync(
        Guid actorMemberId,
        CreateEvaluationFormCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EvaluationFormDto> UpdateFormAsync(
        Guid actorMemberId,
        Guid formId,
        UpdateEvaluationFormCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EvaluationSubmissionReceiptDto> SubmitPeerEvaluationAsync(
        Guid reviewerCandidateId,
        SubmitEvaluationCommand command,
        CancellationToken cancellationToken);

    public Task<EvaluationSubmissionReceiptDto> SubmitMentorEvaluationAsync(
        Guid reviewerMemberId,
        SubmitEvaluationCommand command,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<EvaluationSubmissionDto>> ListResultsAsync(
        Guid actorMemberId,
        Guid? periodId,
        Guid? formId,
        CancellationToken cancellationToken);
}
