using Felion.Domain.Audit;
using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public sealed record UpsertDiscordRoleMappingCommand(
    DiscordRoleMappingKind Kind,
    string SubjectKey,
    long DiscordRoleId,
    string RoleNameSnapshot);

public sealed record DiscordRoleMappingDto(
    Guid Id,
    DiscordRoleMappingKind Kind,
    string SubjectKey,
    long DiscordRoleId,
    string RoleNameSnapshot,
    DateTimeOffset UpdatedAt);

public interface IDiscordRoleMappingStore
{
    public Task<IReadOnlyList<DiscordRoleMapping>> ListAsync(CancellationToken cancellationToken);

    public Task<DiscordRoleMapping?> FindAsync(
        DiscordRoleMappingKind kind,
        string subjectKey,
        bool track,
        CancellationToken cancellationToken);

    public Task UpsertAsync(
        DiscordRoleMapping mapping,
        AuditLog auditLog,
        CancellationToken cancellationToken);
}

public interface IDiscordRoleMappingService
{
    public Task<IReadOnlyList<DiscordRoleMappingDto>> ListAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken);

    public Task<DiscordRoleMappingDto> UpsertAsync(
        Guid actorMemberId,
        UpsertDiscordRoleMappingCommand command,
        string correlationId,
        CancellationToken cancellationToken);
}
