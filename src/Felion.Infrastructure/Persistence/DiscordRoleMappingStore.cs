using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class DiscordRoleMappingStore(FelionDbContext dbContext) : IDiscordRoleMappingStore
{
    public async Task<IReadOnlyList<DiscordRoleMapping>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.DiscordRoleMappings
            .AsNoTracking()
            .OrderBy(mapping => mapping.Kind)
            .ThenBy(mapping => mapping.SubjectKey)
            .ToListAsync(cancellationToken);
    }

    public Task<DiscordRoleMapping?> FindAsync(
        DiscordRoleMappingKind kind,
        string subjectKey,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.DiscordRoleMappings.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            mapping => mapping.Kind == kind && mapping.SubjectKey == subjectKey,
            cancellationToken);
    }

    public async Task UpsertAsync(
        DiscordRoleMapping mapping,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        if (dbContext.Entry(mapping).State == EntityState.Detached)
        {
            dbContext.DiscordRoleMappings.Add(mapping);
        }

        dbContext.AuditLogs.Add(auditLog);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new DiscordRoleMappingConflictException(
                "The Discord role mapping key or role ID is already mapped by another request.");
        }
    }
}
