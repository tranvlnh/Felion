using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class MemberStore(FelionDbContext dbContext) : IMemberStore
{
    public async Task<Member?> FindByIdAsync(
        Guid memberId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Members.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(member => member.Id == memberId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        MemberStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Members.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(member => member.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(member => member.StudentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<MemberIdentityLookup> FindIdentityConflictsAsync(
        IReadOnlyCollection<string> studentIds,
        IReadOnlyCollection<string> clubEmails,
        CancellationToken cancellationToken)
    {
        var students = studentIds.Count == 0
            ? []
            : await dbContext.Members
                .AsNoTracking()
                .Where(member => studentIds.Contains(member.StudentId))
                .Select(member => member.StudentId)
                .ToListAsync(cancellationToken);
        var emails = clubEmails.Count == 0
            ? []
            : await dbContext.Members
                .AsNoTracking()
                .Where(member => clubEmails.Contains(member.ClubEmail))
                .Select(member => member.ClubEmail)
                .ToListAsync(cancellationToken);

        return new MemberIdentityLookup(
            students.ToHashSet(StringComparer.Ordinal),
            emails.ToHashSet(StringComparer.Ordinal));
    }

    public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        return dbContext.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(department => department.Id == departmentId, cancellationToken);
    }

    public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken)
    {
        return dbContext.Generations
            .AsNoTracking()
            .SingleOrDefaultAsync(generation => generation.Id == generationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Departments.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Generations.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task AddAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
    {
        dbContext.Members.Add(member);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task AddRangeAsync(
        IReadOnlyCollection<MemberAuditEntry> entries,
        CancellationToken cancellationToken)
    {
        dbContext.Members.AddRange(entries.Select(entry => entry.Member));
        dbContext.AuditLogs.AddRange(entries.Select(entry => entry.AuditLog));
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
            throw new MemberConflictException("Member StudentId or ClubEmail already exists.");
        }
    }
}
