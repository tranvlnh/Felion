using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class DiscordLinkStore(FelionDbContext dbContext) : IDiscordLinkStore
{
    public async Task<IReadOnlyList<DiscordLinkSubject>> FindEligibleSubjectsByStudentIdAsync(
        string normalizedStudentId,
        CancellationToken cancellationToken)
    {
        var members = await dbContext.Members
            .AsNoTracking()
            .Where(member => member.Status == MemberStatus.Active && member.StudentId == normalizedStudentId)
            .Select(member => new DiscordLinkSubject(
                member.Id,
                DiscordIdentitySubjectType.Member,
                member.StudentId,
                member.FullName))
            .ToListAsync(cancellationToken);
        var candidates = await dbContext.ProbationCandidates
            .AsNoTracking()
            .Where(candidate => candidate.Status == ProbationCandidateStatus.Active
                && candidate.StudentId == normalizedStudentId)
            .Select(candidate => new DiscordLinkSubject(
                candidate.Id,
                DiscordIdentitySubjectType.Probation,
                candidate.StudentId,
                candidate.FullName))
            .ToListAsync(cancellationToken);

        return [.. members, .. candidates];
    }

    public Task<DiscordIdentityLink?> FindByStudentIdAsync(
        string normalizedStudentId,
        CancellationToken cancellationToken)
    {
        return dbContext.DiscordIdentityLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(link => link.StudentId == normalizedStudentId, cancellationToken);
    }

    public Task<DiscordIdentityLink?> FindByDiscordUserIdAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.DiscordIdentityLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(link => link.DiscordUserId == discordUserId, cancellationToken);
    }

    public async Task AddAsync(
        DiscordIdentityLink link,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.DiscordIdentityLinks.Add(link);
        dbContext.AuditLogs.Add(auditLog);

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
