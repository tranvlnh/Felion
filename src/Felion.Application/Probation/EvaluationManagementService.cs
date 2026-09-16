using System.Text.Json;
using Felion.Application.Hardening;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Evaluation;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Probation;

public sealed class EvaluationManagementService(
    IEvaluationStore store,
    IMemberStore memberStore,
    IEvaluationDefaultsProvider defaultsProvider,
    IProbationTeamStore? probationTeamStore,
    IRateLimitGate rateLimitGate) : IEvaluationManagementService
{
    public async Task<IReadOnlyList<EvaluationPeriodDto>> ListPeriodsAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        return (await store.ListPeriodsAsync(cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<EvaluationPeriodDto> CreatePeriodAsync(
        Guid actorMemberId,
        CreateEvaluationPeriodCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);

        EvaluationPeriod period;
        try
        {
            period = EvaluationPeriod.Create(command.Name, command.StartsAt, command.EndsAt);
        }
        catch (DomainException exception)
        {
            throw new EvaluationValidationException(exception.Message);
        }

        var audit = CreateAudit(
            actorMemberId,
            "EvaluationPeriodCreated",
            period.Id,
            correlationId,
            after: Snapshot(period));
        await store.AddPeriodAsync(period, audit, cancellationToken);
        return ToDto(new EvaluationPeriodView(
            period.Id,
            period.Name,
            period.StartsAt,
            period.EndsAt,
            period.Status,
            [],
            period.CreatedAt,
            period.UpdatedAt));
    }

    public async Task<EvaluationPeriodDto> OpenPeriodAsync(
        Guid actorMemberId,
        Guid periodId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var period = await store.FindPeriodAsync(periodId, track: true, cancellationToken)
            ?? throw new EvaluationPeriodNotFoundException(periodId);
        var before = Snapshot(period);
        try
        {
            period.Open();
        }
        catch (DomainException exception)
        {
            throw new EvaluationValidationException(exception.Message);
        }

        await store.UpdatePeriodAsync(
            period,
            CreateAudit(
                actorMemberId,
                "EvaluationPeriodOpened",
                period.Id,
                correlationId,
                before,
                Snapshot(period)),
            cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<EvaluationPeriodDto> ClosePeriodAsync(
        Guid actorMemberId,
        Guid periodId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var period = await store.FindPeriodAsync(periodId, track: true, cancellationToken)
            ?? throw new EvaluationPeriodNotFoundException(periodId);
        var before = Snapshot(period);
        try
        {
            period.Close();
        }
        catch (DomainException exception)
        {
            throw new EvaluationValidationException(exception.Message);
        }

        await store.UpdatePeriodAsync(
            period,
            CreateAudit(
                actorMemberId,
                "EvaluationPeriodClosed",
                period.Id,
                correlationId,
                before,
                Snapshot(period)),
            cancellationToken);
        return await GetPeriodAsync(period.Id, cancellationToken);
    }

    public async Task<EvaluationFormDto> CreateFormAsync(
        Guid actorMemberId,
        CreateEvaluationFormCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        _ = await store.FindPeriodAsync(command.PeriodId, track: false, cancellationToken)
            ?? throw new EvaluationPeriodNotFoundException(command.PeriodId);

        EvaluationForm form;
        try
        {
            form = EvaluationForm.Create(command.PeriodId, command.Name, command.ReviewerType);
        }
        catch (DomainException exception)
        {
            throw new EvaluationValidationException(exception.Message);
        }

        var questions = CreateQuestions(form.Id, command.Questions);
        var audit = CreateAudit(
            actorMemberId,
            "EvaluationFormCreated",
            form.Id,
            correlationId,
            after: Snapshot(form, questions));
        await store.AddFormAsync(form, questions, audit, cancellationToken);
        return ToDto(new EvaluationFormView(
            form.Id,
            form.PeriodId,
            form.Name,
            form.ReviewerType,
            form.IsActive,
            questions.Select(ToView).OrderBy(question => question.Order).ToArray(),
            form.CreatedAt,
            form.UpdatedAt));
    }

    public async Task<EvaluationFormDto> UpdateFormAsync(
        Guid actorMemberId,
        Guid formId,
        UpdateEvaluationFormCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        if (command.Name is null && command.IsActive is null && command.Questions is null)
        {
            throw new EvaluationValidationException("At least one evaluation form field must be provided.");
        }

        var form = await store.FindFormAsync(formId, track: true, cancellationToken)
            ?? throw new EvaluationFormNotFoundException(formId);
        var existingQuestions = (await store.ListQuestionsAsync(formId, track: true, cancellationToken)).ToArray();
        var before = Snapshot(form, existingQuestions);
        IReadOnlyList<EvaluationQuestion> addedQuestions = [];
        IReadOnlyList<EvaluationQuestion> removedQuestions = [];

        try
        {
            form.Update(command.Name, command.IsActive);
            if (command.Questions is not null)
            {
                (addedQuestions, removedQuestions) = UpdateQuestions(form.Id, existingQuestions, command.Questions);
            }
        }
        catch (DomainException exception)
        {
            throw new EvaluationValidationException(exception.Message);
        }

        var afterQuestions = existingQuestions
            .Except(removedQuestions)
            .Concat(addedQuestions)
            .OrderBy(question => question.Order)
            .ToArray();
        await store.UpdateFormAsync(
            form,
            addedQuestions,
            removedQuestions,
            CreateAudit(
                actorMemberId,
                "EvaluationFormUpdated",
                form.Id,
                correlationId,
                before,
                Snapshot(form, afterQuestions)),
            cancellationToken);

        return ToDto(new EvaluationFormView(
            form.Id,
            form.PeriodId,
            form.Name,
            form.ReviewerType,
            form.IsActive,
            afterQuestions.Select(ToView).ToArray(),
            form.CreatedAt,
            form.UpdatedAt));
    }

    public Task<EvaluationSubmissionReceiptDto> SubmitPeerEvaluationAsync(
        Guid reviewerCandidateId,
        SubmitEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(
            EvaluationReviewerType.Peer,
            reviewerCandidateId,
            command,
            cancellationToken);
    }

    public Task<EvaluationSubmissionReceiptDto> SubmitMentorEvaluationAsync(
        Guid reviewerMemberId,
        SubmitEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(
            EvaluationReviewerType.Mentor,
            reviewerMemberId,
            command,
            cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationSubmissionDto>> ListResultsAsync(
        Guid actorMemberId,
        Guid? periodId,
        Guid? formId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var submissions = await store.ListSubmissionViewsAsync(periodId, formId, cancellationToken);
        return submissions.Select(ToDto).ToArray();
    }

    private async Task<EvaluationSubmissionReceiptDto> SubmitAsync(
        EvaluationReviewerType reviewerType,
        Guid reviewerId,
        SubmitEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        if (reviewerId == Guid.Empty)
        {
            throw new EvaluationSubmissionValidationException("Reviewer is required.");
        }

        var rateLimit = await rateLimitGate.TryAcquireAsync(
            RateLimitOperation.EvaluationSubmission,
            $"{reviewerType}:{reviewerId:N}",
            cancellationToken);
        if (!rateLimit.IsAcquired)
        {
            throw new RateLimitExceededException(RateLimitOperation.EvaluationSubmission, rateLimit.RetryAfter);
        }

        var form = await store.FindFormAsync(command.FormId, track: false, cancellationToken)
            ?? throw new EvaluationFormNotFoundException(command.FormId);
        var period = await store.FindPeriodAsync(form.PeriodId, track: false, cancellationToken)
            ?? throw new EvaluationPeriodNotFoundException(form.PeriodId);
        if (period.Status != EvaluationPeriodStatus.Open)
        {
            throw new EvaluationSubmissionValidationException("Only an open evaluation period accepts submissions.");
        }

        if (!form.IsActive)
        {
            throw new EvaluationSubmissionValidationException("Only an active evaluation form accepts submissions.");
        }

        if (form.ReviewerType != reviewerType)
        {
            throw new EvaluationSubmissionValidationException(
                $"This form accepts {form.ReviewerType} evaluations only.");
        }

        var probationStore = probationTeamStore
            ?? throw new EvaluationSubmissionValidationException("Probation evaluation storage is unavailable.");
        var target = await probationStore.FindCandidateAsync(
            command.TargetCandidateId,
            track: false,
            cancellationToken)
            ?? throw new ProbationCandidateNotFoundException(command.TargetCandidateId);
        if (target.Status != ProbationCandidateStatus.Active)
        {
            throw new EvaluationSubmissionValidationException("Only an active target candidate can be evaluated.");
        }

        string reviewerStudentId;
        string reviewerName;
        Guid? reviewerMemberId;
        Guid? reviewerCandidateId;
        switch (reviewerType)
        {
            case EvaluationReviewerType.Peer:
                {
                    var reviewer = await probationStore.FindCandidateAsync(
                        reviewerId,
                        track: false,
                        cancellationToken)
                        ?? throw new ProbationCandidateNotFoundException(reviewerId);
                    if (reviewer.Status != ProbationCandidateStatus.Active)
                    {
                        throw new EvaluationSubmissionValidationException("Only an active candidate can submit a peer evaluation.");
                    }

                    if (reviewer.Id == target.Id)
                    {
                        throw new EvaluationSubmissionValidationException("A candidate cannot evaluate themselves.");
                    }

                    if (reviewer.TeamId is null || reviewer.TeamId != target.TeamId)
                    {
                        throw new EvaluationSubmissionValidationException(
                            "A peer evaluation must target another active candidate in the same team.");
                    }

                    reviewerStudentId = reviewer.StudentId;
                    reviewerName = reviewer.FullName;
                    reviewerMemberId = null;
                    reviewerCandidateId = reviewer.Id;
                    break;
                }
            case EvaluationReviewerType.Mentor:
                {
                    var reviewer = await memberStore.FindByIdAsync(
                        reviewerId,
                        track: false,
                        cancellationToken)
                        ?? throw new EvaluationSubmissionValidationException("Mentor member was not found.");
                    if (reviewer.Status != MemberStatus.Active)
                    {
                        throw new EvaluationSubmissionValidationException("Only an active Member can submit a mentor evaluation.");
                    }

                    if (target.TeamId is null
                        || await probationStore.FindMentorAsync(
                            target.TeamId.Value,
                            reviewer.Id,
                            track: false,
                            cancellationToken) is null)
                    {
                        throw new EvaluationSubmissionValidationException(
                            "A mentor may evaluate candidates only in teams they mentor.");
                    }

                    reviewerStudentId = reviewer.StudentId;
                    reviewerName = reviewer.FullName;
                    reviewerMemberId = reviewer.Id;
                    reviewerCandidateId = null;
                    break;
                }
            default:
                throw new EvaluationSubmissionValidationException("Unknown evaluation reviewer type.");
        }

        var existing = await store.FindSubmissionAsync(
            form.Id,
            reviewerMemberId,
            reviewerCandidateId,
            target.Id,
            track: true,
            cancellationToken);
        EvaluationSubmission submission;
        try
        {
            submission = existing ?? EvaluationSubmission.Create(
                form.Id,
                period.Id,
                reviewerType,
                reviewerMemberId,
                reviewerCandidateId,
                reviewerStudentId,
                reviewerName,
                target.Id,
                target.StudentId,
                target.FullName);
            if (existing is not null)
            {
                submission.UpdateSnapshots(
                    target.Id,
                    reviewerStudentId,
                    reviewerName,
                    target.StudentId,
                    target.FullName);
            }
        }
        catch (DomainException exception)
        {
            throw new EvaluationSubmissionValidationException(exception.Message);
        }

        var questions = await store.ListQuestionsAsync(form.Id, track: false, cancellationToken);
        var answers = CreateAnswers(submission.Id, questions, command.Answers);
        await store.SaveSubmissionAsync(submission, answers, existing is null, cancellationToken);
        return new EvaluationSubmissionReceiptDto(
            submission.Id,
            submission.SubmittedAt,
            submission.UpdatedAt);
    }

    private static EvaluationAnswer[] CreateAnswers(
        Guid submissionId,
        IReadOnlyList<EvaluationQuestion> questions,
        IReadOnlyList<EvaluationAnswerCommand> commands)
    {
        var questionsById = questions.ToDictionary(question => question.Id);
        var commandsById = new Dictionary<Guid, EvaluationAnswerCommand>();
        foreach (var command in commands ?? [])
        {
            if (!commandsById.TryAdd(command.QuestionId, command))
            {
                throw new EvaluationSubmissionValidationException(
                    $"Question '{command.QuestionId}' was answered more than once.");
            }

            if (!questionsById.ContainsKey(command.QuestionId))
            {
                throw new EvaluationSubmissionValidationException(
                    $"Question '{command.QuestionId}' was not found in this form.");
            }
        }

        foreach (var question in questions)
        {
            if (!question.IsRequired || HasValue(commandsById.GetValueOrDefault(question.Id)))
            {
                continue;
            }

            throw new EvaluationSubmissionValidationException(
                $"Required question '{question.Id}' must be answered.");
        }

        var answers = new List<EvaluationAnswer>(commandsById.Count);
        foreach (var command in commandsById.Values)
        {
            if (!HasValue(command))
            {
                continue;
            }

            try
            {
                answers.Add(EvaluationAnswer.Create(
                    submissionId,
                    questionsById[command.QuestionId],
                    command.ScoreValue,
                    command.TextValue));
            }
            catch (DomainException exception)
            {
                throw new EvaluationSubmissionValidationException(exception.Message);
            }
        }

        return answers.ToArray();
    }

    private static bool HasValue(EvaluationAnswerCommand? command)
    {
        return command is not null
            && (command.ScoreValue is not null || !string.IsNullOrWhiteSpace(command.TextValue));
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
            throw new EvaluationAccessDeniedException();
        }
    }

    private async Task<EvaluationPeriodDto> GetPeriodAsync(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var view = await store.FindPeriodViewAsync(periodId, cancellationToken)
            ?? throw new EvaluationPeriodNotFoundException(periodId);
        return ToDto(view);
    }

    private EvaluationQuestion[] CreateQuestions(
        Guid formId,
        IReadOnlyList<EvaluationQuestionCommand> commands)
    {
        if (commands is null || commands.Count == 0)
        {
            throw new EvaluationValidationException("An evaluation form must contain at least one question.");
        }

        var questions = commands.Select(command => CreateQuestion(formId, command)).ToArray();
        EnsureUniqueQuestionOrders(questions.Select(question => question.Order));
        return questions;
    }

    private (IReadOnlyList<EvaluationQuestion> Added, IReadOnlyList<EvaluationQuestion> Removed) UpdateQuestions(
        Guid formId,
        IReadOnlyList<EvaluationQuestion> existingQuestions,
        IReadOnlyList<EvaluationQuestionCommand> commands)
    {
        if (commands.Count == 0)
        {
            throw new EvaluationValidationException("An evaluation form must contain at least one question.");
        }

        var existingById = existingQuestions.ToDictionary(question => question.Id);
        var retainedIds = new HashSet<Guid>();
        var added = new List<EvaluationQuestion>();
        foreach (var command in commands)
        {
            if (command.Id is null)
            {
                added.Add(CreateQuestion(formId, command));
                continue;
            }

            if (!existingById.TryGetValue(command.Id.Value, out var question))
            {
                throw new EvaluationValidationException($"Evaluation question '{command.Id}' was not found in this form.");
            }

            if (!retainedIds.Add(question.Id))
            {
                throw new EvaluationValidationException($"Evaluation question '{question.Id}' was provided more than once.");
            }

            question.Update(
                command.Order,
                command.Prompt,
                command.Type,
                command.IsRequired,
                ResolveScoreMin(command),
                ResolveScoreMax(command),
                ResolveTextMaxLength(command));
        }

        EnsureUniqueQuestionOrders(existingQuestions.Where(question => retainedIds.Contains(question.Id)).Select(question => question.Order)
            .Concat(added.Select(question => question.Order)));
        return (added, existingQuestions.Where(question => !retainedIds.Contains(question.Id)).ToArray());
    }

    private EvaluationQuestion CreateQuestion(Guid formId, EvaluationQuestionCommand command)
    {
        try
        {
            return EvaluationQuestion.Create(
                formId,
                command.Order,
                command.Prompt,
                command.Type,
                command.IsRequired,
                ResolveScoreMin(command),
                ResolveScoreMax(command),
                ResolveTextMaxLength(command));
        }
        catch (DomainException exception)
        {
            throw new EvaluationValidationException(exception.Message);
        }
    }

    private decimal? ResolveScoreMin(EvaluationQuestionCommand command)
    {
        return command.Type == EvaluationQuestionType.Score
            ? command.ScoreMin ?? defaultsProvider.GetDefaults().DefaultScoreMin
            : null;
    }

    private decimal? ResolveScoreMax(EvaluationQuestionCommand command)
    {
        return command.Type == EvaluationQuestionType.Score
            ? command.ScoreMax ?? defaultsProvider.GetDefaults().DefaultScoreMax
            : null;
    }

    private int? ResolveTextMaxLength(EvaluationQuestionCommand command)
    {
        return command.Type == EvaluationQuestionType.Text
            ? command.TextMaxLength ?? defaultsProvider.GetDefaults().DefaultTextMaxLength
            : null;
    }

    private static void EnsureUniqueQuestionOrders(IEnumerable<int> orders)
    {
        var values = orders.ToArray();
        if (values.Any(order => order <= 0))
        {
            throw new EvaluationValidationException("Question order must be positive.");
        }

        if (values.Distinct().Count() != values.Length)
        {
            throw new EvaluationValidationException("Question order must be unique within a form.");
        }
    }

    private static AuditLog CreateAudit(
        Guid actorMemberId,
        string action,
        Guid entityId,
        string correlationId,
        string? before = null,
        string? after = null)
    {
        return AuditLog.Create(
            AuditActorType.WebMember,
            actorMemberId,
            actorDiscordUserId: null,
            action,
            "Evaluation" + (action.Contains("Form", StringComparison.Ordinal) ? "Form" : "Period"),
            entityId,
            correlationId,
            beforeJson: before,
            afterJson: after);
    }

    private static EvaluationPeriodDto ToDto(EvaluationPeriodView period)
    {
        return new EvaluationPeriodDto(
            period.Id,
            period.Name,
            period.StartsAt,
            period.EndsAt,
            period.Status,
            period.Forms.Select(ToDto).ToArray(),
            period.CreatedAt,
            period.UpdatedAt);
    }

    private static EvaluationFormDto ToDto(EvaluationFormView form)
    {
        return new EvaluationFormDto(
            form.Id,
            form.PeriodId,
            form.Name,
            form.ReviewerType,
            form.IsActive,
            form.Questions.Select(ToDto).OrderBy(question => question.Order).ToArray(),
            form.CreatedAt,
            form.UpdatedAt);
    }

    private static EvaluationQuestionDto ToDto(EvaluationQuestionView question)
    {
        return new EvaluationQuestionDto(
            question.Id,
            question.FormId,
            question.Order,
            question.Prompt,
            question.Type,
            question.IsRequired,
            question.ScoreMin,
            question.ScoreMax,
            question.TextMaxLength,
            question.CreatedAt,
            question.UpdatedAt);
    }

    private static EvaluationQuestionView ToView(EvaluationQuestion question)
    {
        return new EvaluationQuestionView(
            question.Id,
            question.FormId,
            question.Order,
            question.Prompt,
            question.Type,
            question.IsRequired,
            question.ScoreMin,
            question.ScoreMax,
            question.TextMaxLength,
            question.CreatedAt,
            question.UpdatedAt);
    }

    private static EvaluationSubmissionDto ToDto(EvaluationSubmissionView submission)
    {
        return new EvaluationSubmissionDto(
            submission.Id,
            submission.FormId,
            submission.PeriodId,
            submission.ReviewerType,
            submission.ReviewerMemberId,
            submission.ReviewerCandidateId,
            submission.ReviewerStudentIdSnapshot,
            submission.ReviewerNameSnapshot,
            submission.TargetCandidateId,
            submission.TargetStudentIdSnapshot,
            submission.TargetNameSnapshot,
            submission.SubmittedAt,
            submission.UpdatedAt,
            submission.Answers.Select(answer => new EvaluationAnswerDto(
                answer.Id,
                answer.QuestionId,
                answer.QuestionPromptSnapshot,
                answer.QuestionTypeSnapshot,
                answer.ScoreValue,
                answer.TextValue,
                answer.CreatedAt)).ToArray());
    }

    private static string Snapshot(EvaluationPeriod period)
    {
        return JsonSerializer.Serialize(new
        {
            period.Id,
            period.Name,
            period.StartsAt,
            period.EndsAt,
            period.Status,
            period.CreatedAt,
            period.UpdatedAt
        });
    }

    private static string Snapshot(
        EvaluationForm form,
        IEnumerable<EvaluationQuestion> questions)
    {
        return JsonSerializer.Serialize(new
        {
            form.Id,
            form.PeriodId,
            form.Name,
            form.ReviewerType,
            form.IsActive,
            form.CreatedAt,
            form.UpdatedAt,
            Questions = questions.Select(question => new
            {
                question.Id,
                question.FormId,
                question.Order,
                question.Prompt,
                question.Type,
                question.IsRequired,
                question.ScoreMin,
                question.ScoreMax,
                question.TextMaxLength,
                question.CreatedAt,
                question.UpdatedAt
            })
        });
    }
}
