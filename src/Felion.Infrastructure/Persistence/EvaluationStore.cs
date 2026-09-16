using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class EvaluationStore(FelionDbContext dbContext) : IEvaluationStore
{
    public async Task<IReadOnlyList<EvaluationPeriodView>> ListPeriodsAsync(
        CancellationToken cancellationToken)
    {
        var periods = await dbContext.EvaluationPeriods
            .AsNoTracking()
            .OrderByDescending(period => period.CreatedAt)
            .ToListAsync(cancellationToken);
        return await CreateViewsAsync(periods, cancellationToken);
    }

    public async Task<EvaluationPeriodView?> FindPeriodViewAsync(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        var period = await dbContext.EvaluationPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == periodId, cancellationToken);
        if (period is null)
        {
            return null;
        }

        return (await CreateViewsAsync([period], cancellationToken))[0];
    }

    public Task<EvaluationPeriod?> FindPeriodAsync(
        Guid periodId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EvaluationPeriods.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(period => period.Id == periodId, cancellationToken);
    }

    public Task<EvaluationForm?> FindFormAsync(
        Guid formId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EvaluationForms.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(form => form.Id == formId, cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationQuestion>> ListQuestionsAsync(
        Guid formId,
        bool track,
        CancellationToken cancellationToken)
    {
        IQueryable<EvaluationQuestion> query = dbContext.EvaluationQuestions
            .Where(question => question.FormId == formId)
            .OrderBy(question => question.Order);
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<EvaluationSubmission?> FindSubmissionAsync(
        Guid formId,
        Guid? reviewerMemberId,
        Guid? reviewerCandidateId,
        Guid targetCandidateId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EvaluationSubmissions.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            submission => submission.FormId == formId
                && submission.ReviewerMemberId == reviewerMemberId
                && submission.ReviewerCandidateId == reviewerCandidateId
                && submission.TargetCandidateId == targetCandidateId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationSubmissionView>> ListSubmissionViewsAsync(
        Guid? periodId,
        Guid? formId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EvaluationSubmissions
            .AsNoTracking()
            .AsQueryable();
        if (periodId is not null)
        {
            query = query.Where(submission => submission.PeriodId == periodId.Value);
        }

        if (formId is not null)
        {
            query = query.Where(submission => submission.FormId == formId.Value);
        }

        var submissions = await query
            .OrderByDescending(submission => submission.SubmittedAt)
            .ToListAsync(cancellationToken);
        if (submissions.Count == 0)
        {
            return [];
        }

        var submissionIds = submissions.Select(submission => submission.Id).ToArray();
        var answers = await dbContext.EvaluationAnswers
            .AsNoTracking()
            .Where(answer => submissionIds.Contains(answer.SubmissionId))
            .OrderBy(answer => answer.CreatedAt)
            .ToListAsync(cancellationToken);

        return submissions.Select(submission => new EvaluationSubmissionView(
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
            answers
                .Where(answer => answer.SubmissionId == submission.Id)
                .Select(ToView)
                .ToArray())).ToArray();
    }

    public Task AddPeriodAsync(
        EvaluationPeriod period,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.EvaluationPeriods.Add(period);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task UpdatePeriodAsync(
        EvaluationPeriod period,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task AddFormAsync(
        EvaluationForm form,
        IReadOnlyCollection<EvaluationQuestion> questions,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.EvaluationForms.Add(form);
        dbContext.EvaluationQuestions.AddRange(questions);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task UpdateFormAsync(
        EvaluationForm form,
        IReadOnlyCollection<EvaluationQuestion> addedQuestions,
        IReadOnlyCollection<EvaluationQuestion> removedQuestions,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.EvaluationQuestions.RemoveRange(removedQuestions);
        dbContext.EvaluationQuestions.AddRange(addedQuestions);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public async Task SaveSubmissionAsync(
        EvaluationSubmission submission,
        IReadOnlyCollection<EvaluationAnswer> answers,
        bool isNew,
        CancellationToken cancellationToken)
    {
        if (isNew)
        {
            dbContext.EvaluationSubmissions.Add(submission);
        }
        else
        {
            var existingAnswers = await dbContext.EvaluationAnswers
                .Where(answer => answer.SubmissionId == submission.Id)
                .ToListAsync(cancellationToken);
            dbContext.EvaluationAnswers.RemoveRange(existingAnswers);
        }

        dbContext.EvaluationAnswers.AddRange(answers);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new EvaluationConflictException(
                "The evaluation submission was changed by another request.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new EvaluationConflictException(
                "An evaluation submission already exists for this reviewer, target and form.");
        }
    }

    private async Task<IReadOnlyList<EvaluationPeriodView>> CreateViewsAsync(
        List<EvaluationPeriod> periods,
        CancellationToken cancellationToken)
    {
        if (periods.Count == 0)
        {
            return [];
        }

        var periodIds = periods.Select(period => period.Id).ToArray();
        var forms = await dbContext.EvaluationForms
            .AsNoTracking()
            .Where(form => periodIds.Contains(form.PeriodId))
            .OrderBy(form => form.CreatedAt)
            .ToListAsync(cancellationToken);
        var formIds = forms.Select(form => form.Id).ToArray();
        var questions = formIds.Length == 0
            ? []
            : await dbContext.EvaluationQuestions
                .AsNoTracking()
                .Where(question => formIds.Contains(question.FormId))
                .OrderBy(question => question.Order)
                .ToListAsync(cancellationToken);

        return periods.Select(period => new EvaluationPeriodView(
            period.Id,
            period.Name,
            period.StartsAt,
            period.EndsAt,
            period.Status,
            forms
                .Where(form => form.PeriodId == period.Id)
                .Select(form => new EvaluationFormView(
                    form.Id,
                    form.PeriodId,
                    form.Name,
                    form.ReviewerType,
                    form.IsActive,
                    questions
                        .Where(question => question.FormId == form.Id)
                        .Select(ToView)
                        .ToArray(),
                    form.CreatedAt,
                    form.UpdatedAt))
                .ToArray(),
            period.CreatedAt,
            period.UpdatedAt)).ToArray();
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new EvaluationConflictException("The evaluation period, form or question was changed by another request.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new EvaluationConflictException("Question order conflicts with another question in this form.");
        }
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

    private static EvaluationAnswerView ToView(EvaluationAnswer answer)
    {
        return new EvaluationAnswerView(
            answer.Id,
            answer.QuestionId,
            answer.QuestionPromptSnapshot,
            answer.QuestionTypeSnapshot,
            answer.ScoreValue,
            answer.TextValue,
            answer.CreatedAt);
    }
}
