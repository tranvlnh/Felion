using Felion.Application.Discord;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;

namespace Felion.Infrastructure.Persistence;

internal sealed class DiscordSyncJobStore(FelionDbContext dbContext) : IDiscordSyncJobStore
{
    public async Task<DiscordSyncJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var job = await dbContext.DiscordSyncJobs
            .FromSqlRaw(
                """
                SELECT *
                FROM discord_sync_jobs
                WHERE status IN ('Pending', 'Failed')
                ORDER BY created_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        job.MarkRunning();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    public async Task<DiscordRoleSyncTarget?> FindTargetAsync(
        DiscordSyncJob job,
        CancellationToken cancellationToken)
    {
        if (job.SubjectType == DiscordIdentitySubjectType.Member)
        {
            return await (
                from link in dbContext.DiscordIdentityLinks.AsNoTracking()
                join member in dbContext.Members.AsNoTracking()
                    on link.SubjectId equals member.Id
                where link.SubjectType == DiscordIdentitySubjectType.Member
                    && link.SubjectId == job.SubjectId
                    && member.Status == MemberStatus.Active
                select new DiscordRoleSyncTarget(
                    link.DiscordUserId,
                    link.SubjectType,
                    member.Position,
                    member.DepartmentId,
                    member.GenerationId,
                    ProbationTeamId: null))
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (job.SubjectType == DiscordIdentitySubjectType.Probation)
        {
            return await (
                from link in dbContext.DiscordIdentityLinks.AsNoTracking()
                join candidate in dbContext.ProbationCandidates.AsNoTracking()
                    on link.SubjectId equals candidate.Id
                where link.SubjectType == DiscordIdentitySubjectType.Probation
                    && link.SubjectId == job.SubjectId
                    && candidate.Status == ProbationCandidateStatus.Active
                select new DiscordRoleSyncTarget(
                    link.DiscordUserId,
                    link.SubjectType,
                    Position: null,
                    candidate.DepartmentId,
                    candidate.GenerationId,
                    candidate.TeamId))
                .SingleOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    public async Task<IReadOnlyList<DiscordRoleMapping>> ListRoleMappingsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.DiscordRoleMappings
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task SaveJobAsync(DiscordSyncJob job, CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
