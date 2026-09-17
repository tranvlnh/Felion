using Felion.Application.Bootstrap;
using Felion.Domain.Audit;
using Felion.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Felion.Infrastructure.Persistence;

internal sealed class BootstrapStore(FelionDbContext dbContext) : IBootstrapStore
{
    public Task<bool> HasAnyMembersAsync(CancellationToken cancellationToken)
    {
        return dbContext.Members.AnyAsync(cancellationToken);
    }

    public Task<Department?> FindDepartmentByIdAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        return dbContext.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == departmentId, cancellationToken);
    }

    public Task<Generation?> FindGenerationByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return dbContext.Generations
            .SingleOrDefaultAsync(g => g.Code == code, cancellationToken);
    }

    public Task SaveBootstrapAsync(
        Generation? newGeneration,
        Member adminMember,
        IReadOnlyCollection<AuditLog> auditLogs,
        CancellationToken cancellationToken)
    {
        if (newGeneration is not null)
        {
            dbContext.Generations.Add(newGeneration);
        }

        dbContext.Members.Add(adminMember);
        dbContext.AuditLogs.AddRange(auditLogs);

        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
