using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class ProbationTeamStore(FelionDbContext dbContext) : IProbationTeamStore
{
    public async Task<IReadOnlyList<ProbationTeamView>> ListAsync(
        CancellationToken cancellationToken)
    {
        var teams = await dbContext.ProbationTeams
            .AsNoTracking()
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);
        return await CreateViewsAsync(teams, cancellationToken);
    }

    public async Task<ProbationTeamView?> FindViewAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var team = await dbContext.ProbationTeams
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == teamId, cancellationToken);
        if (team is null)
        {
            return null;
        }

        return (await CreateViewsAsync([team], cancellationToken))[0];
    }

    public Task<ProbationTeam?> FindTeamAsync(
        Guid teamId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProbationTeams.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(team => team.Id == teamId, cancellationToken);
    }

    public Task<ProbationCandidate?> FindCandidateAsync(
        Guid candidateId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProbationCandidates.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(candidate => candidate.Id == candidateId, cancellationToken);
    }

    public Task<TeamMentor?> FindMentorAsync(
        Guid teamId,
        Guid memberId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TeamMentors.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            mentor => mentor.TeamId == teamId && mentor.MemberId == memberId,
            cancellationToken);
    }

    public Task<long?> FindCandidateDiscordUserIdAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        return dbContext.DiscordIdentityLinks
            .AsNoTracking()
            .Where(link => link.SubjectType == DiscordIdentitySubjectType.Probation
                && link.SubjectId == candidateId)
            .Select(link => (long?)link.DiscordUserId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task AddTeamAsync(
        ProbationTeam team,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.ProbationTeams.Add(team);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task UpdateTeamAsync(
        ProbationTeam team,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task SaveCandidateAssignmentAsync(
        ProbationCandidate candidate,
        AuditLog auditLog,
        DiscordSyncJob? syncJob,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        if (syncJob is not null)
        {
            dbContext.DiscordSyncJobs.Add(syncJob);
        }

        return SaveChangesAsync(cancellationToken);
    }

    public Task AddMentorAsync(
        TeamMentor mentor,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.TeamMentors.Add(mentor);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task RemoveMentorAsync(
        TeamMentor mentor,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.TeamMentors.Remove(mentor);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ProbationTeamView>> CreateViewsAsync(
        List<ProbationTeam> teams,
        CancellationToken cancellationToken)
    {
        if (teams.Count == 0)
        {
            return [];
        }

        var teamIds = teams.Select(team => team.Id).ToArray();
        var candidateAssignments = await dbContext.ProbationCandidates
            .AsNoTracking()
            .Where(candidate => candidate.TeamId.HasValue && teamIds.Contains(candidate.TeamId.Value))
            .Select(candidate => new { candidate.TeamId, candidate.Id })
            .ToListAsync(cancellationToken);
        var mentors = await dbContext.TeamMentors
            .AsNoTracking()
            .Where(mentor => teamIds.Contains(mentor.TeamId))
            .Select(mentor => new { mentor.TeamId, mentor.MemberId })
            .ToListAsync(cancellationToken);

        return teams.Select(team => new ProbationTeamView(
            team.Id,
            team.Name,
            team.IsActive,
            candidateAssignments
                .Where(assignment => assignment.TeamId == team.Id)
                .Select(assignment => assignment.Id)
                .ToArray(),
            mentors
                .Where(mentor => mentor.TeamId == team.Id)
                .Select(mentor => mentor.MemberId)
                .ToArray(),
            team.CreatedAt,
            team.UpdatedAt)).ToArray();
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ProbationTeamConflictException(
                "The probation team or candidate was changed by another request.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new ProbationTeamConflictException(
                "The mentor is already assigned or the team assignment conflicts with another request.");
        }
    }
}
