using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class ProbationCandidateStore(FelionDbContext dbContext) : IProbationCandidateStore
{
    public async Task<(IReadOnlyList<ProbationCandidateView> Items, int TotalCount)> ListAsync(
        ListProbationCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var candidates = ApplyFilters(dbContext.ProbationCandidates.AsNoTracking(), query);
        var totalCount = await candidates.CountAsync(cancellationToken);
        var items = await CreateViewsQuery(candidates.OrderBy(candidate => candidate.StudentId))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<ProbationCandidateView?> FindViewAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        return CreateViewsQuery(dbContext.ProbationCandidates.AsNoTracking())
            .SingleOrDefaultAsync(view => view.Candidate.Id == candidateId, cancellationToken);
    }

    public Task<ProbationCandidate?> FindCandidateAsync(
        Guid candidateId,
        bool track,
        CancellationToken cancellationToken)
    {
        var candidates = dbContext.ProbationCandidates.AsQueryable();
        if (!track)
        {
            candidates = candidates.AsNoTracking();
        }

        return candidates.SingleOrDefaultAsync(candidate => candidate.Id == candidateId, cancellationToken);
    }

    public Task<DiscordIdentityLink?> FindIdentityLinkAsync(
        Guid candidateId,
        bool track,
        CancellationToken cancellationToken)
    {
        var links = dbContext.DiscordIdentityLinks.AsQueryable();
        if (!track)
        {
            links = links.AsNoTracking();
        }

        return links.SingleOrDefaultAsync(
            link => link.SubjectType == DiscordIdentitySubjectType.Probation
                && link.SubjectId == candidateId,
            cancellationToken);
    }

    public async Task<bool> IsStudentIdTakenAsync(
        string studentId,
        Guid? excludedCandidateId,
        CancellationToken cancellationToken)
    {
        var candidateQuery = dbContext.ProbationCandidates
            .AsNoTracking()
            .Where(candidate => candidate.StudentId == studentId);
        if (excludedCandidateId is not null)
        {
            candidateQuery = candidateQuery.Where(candidate => candidate.Id != excludedCandidateId.Value);
        }

        return await candidateQuery.AnyAsync(cancellationToken)
            || await dbContext.Members
                .AsNoTracking()
                .AnyAsync(member => member.StudentId == studentId, cancellationToken);
    }

    public Task<ProbationTeam?> FindTeamAsync(
        Guid teamId,
        bool track,
        CancellationToken cancellationToken)
    {
        var teams = dbContext.ProbationTeams.AsQueryable();
        if (!track)
        {
            teams = teams.AsNoTracking();
        }

        return teams.SingleOrDefaultAsync(team => team.Id == teamId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProbationCandidateTeamReference>> ListTeamsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.ProbationTeams
            .AsNoTracking()
            .OrderBy(team => team.Name)
            .Select(team => new ProbationCandidateTeamReference(team.Id, team.Name, team.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProbationMentorOption>> SearchActiveMentorsAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var members = dbContext.Members
            .AsNoTracking()
            .Where(member => member.Status == MemberStatus.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            members = members.Where(member =>
                EF.Functions.ILike(member.FullName, pattern)
                || EF.Functions.ILike(member.StudentId, pattern));
        }

        return await members
            .OrderBy(member => member.FullName)
            .ThenBy(member => member.StudentId)
            .Take(limit)
            .Select(member => new ProbationMentorOption(member.Id, member.StudentId, member.FullName))
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(
        ProbationCandidate candidate,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.ProbationCandidates.Add(candidate);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task SaveUpdateAsync(
        ProbationCandidate candidate,
        DiscordIdentityLink? identityLink,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        if (identityLink is not null)
        {
            dbContext.Entry(identityLink).State = EntityState.Modified;
        }

        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task SaveTeamChangeAsync(
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

    private static IQueryable<ProbationCandidate> ApplyFilters(
        IQueryable<ProbationCandidate> candidates,
        ListProbationCandidatesQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            candidates = candidates.Where(candidate =>
                EF.Functions.ILike(candidate.StudentId, pattern)
                || EF.Functions.ILike(candidate.FullName, pattern));
        }

        if (query.DepartmentId is not null)
        {
            candidates = candidates.Where(candidate => candidate.DepartmentId == query.DepartmentId.Value);
        }

        if (query.GenerationId is not null)
        {
            candidates = candidates.Where(candidate => candidate.GenerationId == query.GenerationId.Value);
        }

        if (query.TeamId is not null)
        {
            candidates = candidates.Where(candidate => candidate.TeamId == query.TeamId.Value);
        }
        else if (query.HasTeam is not null)
        {
            candidates = query.HasTeam.Value
                ? candidates.Where(candidate => candidate.TeamId != null)
                : candidates.Where(candidate => candidate.TeamId == null);
        }

        if (query.Status is not null)
        {
            candidates = candidates.Where(candidate => candidate.Status == query.Status.Value);
        }

        return candidates;
    }

    private IQueryable<ProbationCandidateView> CreateViewsQuery(IQueryable<ProbationCandidate> candidates)
    {
        return
            from candidate in candidates
            join department in dbContext.Departments.AsNoTracking() on candidate.DepartmentId equals department.Id
            join generation in dbContext.Generations.AsNoTracking() on candidate.GenerationId equals generation.Id
            join team in dbContext.ProbationTeams.AsNoTracking() on candidate.TeamId equals (Guid?)team.Id into teamJoin
            from team in teamJoin.DefaultIfEmpty()
            join link in dbContext.DiscordIdentityLinks.AsNoTracking().Where(link =>
                link.SubjectType == DiscordIdentitySubjectType.Probation) on candidate.Id equals link.SubjectId into linkJoin
            from link in linkJoin.DefaultIfEmpty()
            select new ProbationCandidateView(
                candidate,
                new ProbationCandidateReference(department.Id, department.Name, department.Slug),
                new ProbationCandidateReference(generation.Id, generation.Name, generation.Code),
                candidate.TeamId == null
                    ? null
                    : new ProbationCandidateTeamReference(candidate.TeamId.GetValueOrDefault(), team.Name, team.IsActive),
                link != null);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ProbationCandidateConflictException(
                "The probation candidate was changed by another request.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new ProbationCandidateConflictException(
                "The StudentId or Discord identity link conflicts with another record.");
        }
    }
}
