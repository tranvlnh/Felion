using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class EvaluationStore(FelionDbContext dbContext) : IEvaluationStore
{
    public async Task<IReadOnlyList<EvaluationPeriod>> ListPeriodsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationPeriods
            .AsNoTracking()
            .OrderByDescending(period => period.CreatedAt)
            .ToListAsync(cancellationToken);
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

    public Task<PeerEvaluation?> FindPeerEvaluationAsync(
        Guid periodId,
        Guid evaluatorCandidateId,
        Guid targetCandidateId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.PeerEvaluations.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(evaluation => evaluation.EvaluationPeriodId == periodId
            && evaluation.EvaluatorCandidateId == evaluatorCandidateId
            && evaluation.TargetCandidateId == targetCandidateId, cancellationToken);
    }

    public Task<MentorEvaluation?> FindMentorEvaluationAsync(
        Guid periodId,
        Guid mentorMemberId,
        Guid targetCandidateId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MentorEvaluations.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(evaluation => evaluation.EvaluationPeriodId == periodId
            && evaluation.MentorMemberId == mentorMemberId
            && evaluation.TargetCandidateId == targetCandidateId, cancellationToken);
    }

    public async Task<IReadOnlyList<PeerEvaluation>> ListPeerEvaluationsAsync(
        Guid periodId,
        Guid? targetCandidateId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.PeerEvaluations
            .AsNoTracking()
            .Where(evaluation => evaluation.EvaluationPeriodId == periodId);
        if (targetCandidateId is not null)
        {
            query = query.Where(evaluation => evaluation.TargetCandidateId == targetCandidateId.Value);
        }

        return await query.OrderBy(evaluation => evaluation.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MentorEvaluation>> ListMentorEvaluationsAsync(
        Guid periodId,
        Guid? targetCandidateId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MentorEvaluations
            .AsNoTracking()
            .Where(evaluation => evaluation.EvaluationPeriodId == periodId);
        if (targetCandidateId is not null)
        {
            query = query.Where(evaluation => evaluation.TargetCandidateId == targetCandidateId.Value);
        }

        return await query.OrderBy(evaluation => evaluation.CreatedAt).ToListAsync(cancellationToken);
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

    public Task SavePeerEvaluationAsync(
        PeerEvaluation evaluation,
        AuditLog auditLog,
        bool isNew,
        CancellationToken cancellationToken)
    {
        if (isNew)
        {
            dbContext.PeerEvaluations.Add(evaluation);
        }

        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task SaveMentorEvaluationAsync(
        MentorEvaluation evaluation,
        AuditLog auditLog,
        bool isNew,
        CancellationToken cancellationToken)
    {
        if (isNew)
        {
            dbContext.MentorEvaluations.Add(evaluation);
        }

        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task ClosePeriodAsync(
        EvaluationPeriod period,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new EvaluationConflictException(
                "Evaluation data was changed by another request. Please retry.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new EvaluationConflictException(
                "An evaluation already exists for this reviewer, target and period.");
        }
    }
}
