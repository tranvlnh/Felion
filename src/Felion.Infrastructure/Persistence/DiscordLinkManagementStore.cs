using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class DiscordLinkManagementStore(FelionDbContext dbContext) : IDiscordLinkManagementStore
{
    public Task<DiscordIdentityLink?> FindByMemberIdAsync(
        Guid memberId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.DiscordIdentityLinks.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            link => link.SubjectType == DiscordIdentitySubjectType.Member
                && link.SubjectId == memberId,
            cancellationToken);
    }

    public Task<DiscordIdentityLink?> FindByDiscordUserIdAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.DiscordIdentityLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(link => link.DiscordUserId == discordUserId, cancellationToken);
    }

    public Task UnlinkAsync(
        DiscordIdentityLink link,
        AuditLog auditLog,
        DiscordSyncJob clearRolesJob,
        CancellationToken cancellationToken)
    {
        dbContext.DiscordIdentityLinks.Remove(link);
        dbContext.AuditLogs.Add(auditLog);
        dbContext.DiscordSyncJobs.Add(clearRolesJob);
        return SaveChangesAsync(cancellationToken);
    }

    public Task RelinkAsync(
        DiscordIdentityLink link,
        AuditLog auditLog,
        IReadOnlyCollection<DiscordSyncJob> syncJobs,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        dbContext.DiscordSyncJobs.AddRange(syncJobs);
        return SaveChangesAsync(cancellationToken);
    }

    public Task EnqueueSyncAsync(
        AuditLog auditLog,
        DiscordSyncJob syncJob,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        dbContext.DiscordSyncJobs.Add(syncJob);
        return SaveChangesAsync(cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new DiscordLinkConflictException(
                "The StudentId or Discord user is already linked by another request.");
        }
    }
}
