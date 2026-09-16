using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class ProbationDecisionStore(FelionDbContext dbContext) : IProbationDecisionStore
{
    public async Task<ProbationDecisionCandidateView?> FindCandidateAsync(
        Guid candidateId,
        bool track,
        CancellationToken cancellationToken)
    {
        var candidates = dbContext.ProbationCandidates.AsQueryable();
        var links = dbContext.DiscordIdentityLinks.AsQueryable();
        if (!track)
        {
            candidates = candidates.AsNoTracking();
            links = links.AsNoTracking();
        }

        var candidate = await candidates.SingleOrDefaultAsync(
            item => item.Id == candidateId,
            cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var link = await links.SingleOrDefaultAsync(
            item => item.SubjectType == DiscordIdentitySubjectType.Probation
                && item.SubjectId == candidateId,
            cancellationToken);
        return new ProbationDecisionCandidateView(candidate, link);
    }

    public Task<bool> MemberStudentIdExistsAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        return dbContext.Members.AnyAsync(member => member.StudentId == studentId, cancellationToken);
    }

    public Task<bool> MemberClubEmailExistsAsync(
        string clubEmail,
        CancellationToken cancellationToken)
    {
        return dbContext.Members.AnyAsync(member => member.ClubEmail == clubEmail, cancellationToken);
    }

    public Task SavePassAsync(
        ProbationCandidate candidate,
        Member member,
        DiscordIdentityLink? identityLink,
        DiscordSyncJob? syncJob,
        AuditLog auditLog,
        bool deleteCandidate,
        CancellationToken cancellationToken)
    {
        dbContext.Members.Add(member);
        if (identityLink is not null)
        {
            dbContext.Entry(identityLink).State = EntityState.Modified;
        }

        if (deleteCandidate)
        {
            dbContext.ProbationCandidates.Remove(candidate);
        }

        if (syncJob is not null)
        {
            dbContext.DiscordSyncJobs.Add(syncJob);
        }

        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task SaveFailAsync(
        ProbationCandidate candidate,
        DiscordIdentityLink? identityLink,
        DiscordSyncJob? kickJob,
        AuditLog auditLog,
        bool deleteCandidate,
        CancellationToken cancellationToken)
    {
        if (identityLink is not null)
        {
            dbContext.DiscordIdentityLinks.Remove(identityLink);
        }

        if (deleteCandidate)
        {
            dbContext.ProbationCandidates.Remove(candidate);
        }

        if (kickJob is not null)
        {
            dbContext.DiscordSyncJobs.Add(kickJob);
        }

        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw new ProbationDecisionConflictException(
                "The probation candidate was changed by another request.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw new ProbationDecisionConflictException(
                "The generated Member identity conflicts with an existing record.");
        }
    }
}
