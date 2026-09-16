using System.Text.Json;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed class DiscordRoleMappingService(
    IDiscordRoleMappingStore store,
    IMemberStore memberStore,
    IDiscordRoleGateway? roleGateway = null) : IDiscordRoleMappingService
{
    public async Task<IReadOnlyList<DiscordRoleMappingDto>> ListAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        await GetAuthorizedActorAsync(actorMemberId, cancellationToken);
        var mappings = await store.ListAsync(cancellationToken);
        return mappings.Select(ToDto).ToArray();
    }

    public async Task<DiscordRoleMappingDto> UpsertAsync(
        Guid actorMemberId,
        UpsertDiscordRoleMappingCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await GetAuthorizedActorAsync(actorMemberId, cancellationToken);

        string normalizedKey;
        try
        {
            normalizedKey = DiscordRoleMapping.NormalizeSubjectKey(command.Kind, command.SubjectKey);
        }
        catch (DomainException exception)
        {
            throw new DiscordRoleMappingValidationException(exception.Message);
        }

        DiscordRoleMapping? mapping;
        try
        {
            var roleName = command.RoleNameSnapshot;
            if (roleGateway is not null)
            {
                var role = await roleGateway.GetRoleAsync(command.DiscordRoleId, cancellationToken);
                roleName = role.Name;
            }

            mapping = await store.FindAsync(
                command.Kind,
                normalizedKey,
                track: true,
                cancellationToken);
            var before = mapping is null ? null : Snapshot(mapping);
            if (mapping is null)
            {
                mapping = DiscordRoleMapping.Create(
                    command.Kind,
                    normalizedKey,
                    command.DiscordRoleId,
                    roleName);
            }
            else
            {
                mapping.UpdateRole(command.DiscordRoleId, roleName);
            }

            var audit = AuditLog.Create(
                AuditActorType.WebMember,
                actorMemberId,
                actorDiscordUserId: null,
                "DiscordRoleMappingUpserted",
                "DiscordRoleMapping",
                mapping.Id,
                correlationId,
                beforeJson: before,
                afterJson: Snapshot(mapping));
            await store.UpsertAsync(mapping, audit, cancellationToken);
        }
        catch (DomainException exception)
        {
            throw new DiscordRoleMappingValidationException(exception.Message);
        }

        return ToDto(mapping);
    }

    private async Task<Member> GetAuthorizedActorAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position != MemberPosition.Admin)
        {
            throw new DiscordRoleMappingAccessDeniedException();
        }

        return actor;
    }

    private static DiscordRoleMappingDto ToDto(DiscordRoleMapping mapping)
    {
        return new DiscordRoleMappingDto(
            mapping.Id,
            mapping.Kind,
            mapping.SubjectKey,
            mapping.DiscordRoleId,
            mapping.RoleNameSnapshot,
            mapping.UpdatedAt);
    }

    private static string Snapshot(DiscordRoleMapping mapping)
    {
        return JsonSerializer.Serialize(new
        {
            mapping.Id,
            mapping.Kind,
            mapping.SubjectKey,
            mapping.DiscordRoleId,
            mapping.RoleNameSnapshot,
            mapping.UpdatedAt
        });
    }
}
