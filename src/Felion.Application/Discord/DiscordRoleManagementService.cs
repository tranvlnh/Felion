using System.Text.Json;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed class DiscordRoleManagementService(
    IDiscordRoleManagementStore store,
    IMemberStore memberStore,
    IDiscordRoleGateway? roleGateway = null) : IDiscordRoleManagementService
{
    public async Task<DiscordRoleSnapshot> CreateAsync(
        Guid actorMemberId,
        CreateDiscordRoleCommand command,
        string correlationId,
        CancellationToken cancellationToken,
        long? actorDiscordUserId = null)
    {
        await EnsureAdminAsync(actorMemberId, cancellationToken);

        string name;
        try
        {
            name = IdentityNormalizer.RequiredText(command.Name, "Name");
        }
        catch (DomainException exception)
        {
            throw new DiscordRoleManagementValidationException(exception.Message);
        }

        if (name.Length > 100)
        {
            throw new DiscordRoleManagementValidationException(
                "Role name must be at most 100 characters.");
        }

        if (roleGateway is null)
        {
            throw new DiscordRoleUnavailableException();
        }

        var role = await roleGateway.CreateRoleAsync(name, cancellationToken);
        var audit = AuditLog.Create(
            actorDiscordUserId is null ? AuditActorType.WebMember : AuditActorType.DiscordMember,
            actorDiscordUserId is null ? actorMemberId : null,
            actorDiscordUserId,
            "DiscordRoleCreated",
            "DiscordRole",
            entityId: null,
            correlationId: correlationId,
            afterJson: JsonSerializer.Serialize(new
            {
                role.Id,
                role.Name
            }));
        await store.AddAuditAsync(audit, cancellationToken);

        return role;
    }

    private async Task EnsureAdminAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position != MemberPosition.Admin)
        {
            throw new DiscordRoleMappingAccessDeniedException();
        }
    }
}
