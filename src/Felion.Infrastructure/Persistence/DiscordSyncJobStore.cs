using Felion.Application.Discord;
using Felion.Domain.Identity;
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
                  AND next_attempt_at <= CURRENT_TIMESTAMP
                ORDER BY next_attempt_at, created_at
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

    public Task SaveJobAsync(DiscordSyncJob job, CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
