using Felion.Application.Discord;
using Felion.Domain.Identity;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Felion.Bot;

[SlashCommand("role", "Manage Felion Discord roles")]
public sealed class RoleAdministrationCommandModule(
    IDiscordAuthorizationService authorizationService,
    IDiscordRoleMappingService mappingService,
    IDiscordRoleMappingSubjectResolver subjectResolver,
    IDiscordRoleManagementService roleManagementService,
    ConfiguredDiscordGuild configuredGuild)
    : AdministrationCommandModuleBase(authorizationService, configuredGuild)
{
    [SubSlashCommand("create", "Create a Discord role in the configured guild")]
    public async Task<InteractionCallbackProperties> CreateAsync(
        [SlashCommandParameter(Description = "The new Discord role name")] string name)
    {
        if (!IsConfiguredGuild())
        {
            return AdministrationCommandResponses.Error(
                "This command is only available in the configured Felion guild.");
        }

        if (!TryGetDiscordUserId(out var discordUserId))
        {
            return AdministrationCommandResponses.Error("The Discord user ID is not supported.");
        }

        try
        {
            var actor = await AuthorizationService.RequireAdminAsync(
                discordUserId,
                CancellationToken.None);
            var role = await roleManagementService.CreateAsync(
                actor.MemberId,
                new CreateDiscordRoleCommand(name),
                CorrelationId(),
                CancellationToken.None,
                actor.DiscordUserId);

            return AdministrationCommandResponses.Success(
                $"✅ Role **{role.Name}** đã được tạo ({role.Id}).");
        }
        catch (Exception exception) when (exception is DiscordAuthorizationException
            or DiscordRoleMappingAccessDeniedException
            or DiscordRoleManagementValidationException
            or DiscordRoleGatewayException)
        {
            return AdministrationCommandResponses.Error(FormatDiscordError(exception));
        }
    }

    [SubSlashCommand("map", "Map a Felion role dimension to an existing Discord role")]
    public async Task<InteractionCallbackProperties> MapAsync(
        [SlashCommandParameter(Description = "The Felion mapping dimension")] DiscordRoleMappingKind kind,
        [SlashCommandParameter(Description = "The Department, Generation or ProbationTeam name; or Position/Probation key")] string subject,
        [SlashCommandParameter(Description = "The existing Discord role")] Role role)
    {
        if (!IsConfiguredGuild())
        {
            return AdministrationCommandResponses.Error(
                "This command is only available in the configured Felion guild.");
        }

        if (!TryGetDiscordUserId(out var discordUserId))
        {
            return AdministrationCommandResponses.Error("The Discord user ID is not supported.");
        }

        if (role.Id == 0 || role.Id > (ulong)long.MaxValue)
        {
            return AdministrationCommandResponses.Error("The Discord role ID is not supported.");
        }

        try
        {
            var actor = await AuthorizationService.RequireAdminAsync(
                discordUserId,
                CancellationToken.None);
            var subjectKey = await subjectResolver.ResolveAsync(
                kind,
                subject,
                CancellationToken.None);
            var mapping = await mappingService.UpsertAsync(
                actor.MemberId,
                new UpsertDiscordRoleMappingCommand(
                    kind,
                    subjectKey,
                    (long)role.Id,
                    role.Name),
                CorrelationId(),
                CancellationToken.None,
                actor.DiscordUserId);

            return AdministrationCommandResponses.Success(
                $"✅ Đã map **{mapping.Kind}:{mapping.SubjectKey}** → **{mapping.RoleNameSnapshot}** ({mapping.DiscordRoleId}).");
        }
        catch (Exception exception) when (exception is DiscordAuthorizationException
            or DiscordRoleMappingAccessDeniedException
            or DiscordRoleMappingValidationException
            or DiscordRoleMappingConflictException
            or DiscordRoleGatewayException)
        {
            return AdministrationCommandResponses.Error(FormatDiscordError(exception));
        }
    }
}
