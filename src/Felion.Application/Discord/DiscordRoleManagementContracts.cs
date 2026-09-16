using Felion.Domain.Audit;

namespace Felion.Application.Discord;

public sealed record CreateDiscordRoleCommand(string Name);

public interface IDiscordRoleManagementStore
{
    public Task AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken);
}

public interface IDiscordRoleManagementService
{
    public Task<DiscordRoleSnapshot> CreateAsync(
        Guid actorMemberId,
        CreateDiscordRoleCommand command,
        string correlationId,
        CancellationToken cancellationToken);
}
