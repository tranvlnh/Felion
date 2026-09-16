using Felion.Application.Discord;
using Felion.Domain.Audit;

namespace Felion.Infrastructure.Persistence;

internal sealed class DiscordRoleManagementStore(FelionDbContext dbContext) : IDiscordRoleManagementStore
{
    public async Task AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
