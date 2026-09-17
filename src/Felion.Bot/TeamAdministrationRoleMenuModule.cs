using Felion.Application.Discord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Felion.Bot;

public sealed class TeamAdministrationRoleMenuModule(
    IDiscordAuthorizationService authorizationService,
    IDiscordRoleMappingService mappingService,
    IDiscordGuildRoleCatalog roleCatalog,
    ConfiguredDiscordGuild configuredGuild)
    : ComponentInteractionModule<RoleMenuInteractionContext>
{
    [ComponentInteraction(TeamInteractionIds.MapRoleSelect)]
    public async Task<InteractionCallbackProperties> MapRoleAsync(
        Domain.Identity.DiscordRoleMappingKind kind,
        string subjectKey)
    {
        var actor = await FindAdminAsync();
        var role = Context.SelectedValues.SingleOrDefault();
        if (actor is null || role is null)
        {
            return AdministrationCommandResponses.Error("Role selection is invalid or unauthorized.");
        }

        if (role.Id is 0 or > long.MaxValue)
        {
            return AdministrationCommandResponses.Error("The Discord role ID is not supported.");
        }

        try
        {
            var catalogRole = (await roleCatalog.ListRolesAsync(CancellationToken.None))
                .SingleOrDefault(candidate => candidate.Id == (long)role.Id);
            if (catalogRole is null || !catalogRole.IsAssignableByBot)
            {
                return AdministrationCommandResponses.Error(
                    "Bot không thể gán role này. Hãy chọn role custom nằm dưới highest role của bot.");
            }

            var mapping = await mappingService.UpsertAsync(
                actor.MemberId,
                new UpsertDiscordRoleMappingCommand(
                    kind,
                    subjectKey,
                    (long)role.Id,
                    role.Name),
                TeamAdministrationComponentSupport.CorrelationId(Context.Interaction.Id),
                CancellationToken.None,
                actor.DiscordUserId);
            return AdministrationCommandResponses.Success(
                $"✅ Đã map **{mapping.Kind}:{mapping.SubjectKey}** → **{mapping.RoleNameSnapshot}**.");
        }
        catch (Exception exception) when (exception is DiscordRoleMappingValidationException
            or DiscordRoleMappingConflictException
            or DiscordRoleMappingAccessDeniedException
            or DiscordRoleGatewayException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    private async Task<DiscordAdminActor?> FindAdminAsync()
    {
        if (!configuredGuild.Matches(Context.Interaction.GuildId)
            || !TeamAdministrationComponentSupport.TryGetDiscordUserId(
                Context.Interaction.User.Id,
                out var discordUserId))
        {
            return null;
        }

        return await authorizationService.FindAdminAsync(discordUserId, CancellationToken.None);
    }
}
