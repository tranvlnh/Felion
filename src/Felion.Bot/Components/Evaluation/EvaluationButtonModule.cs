using Felion.Application.Discord;
using Felion.Application.Probation;
using Felion.Bot.Commands;
using Felion.Bot.Configuration;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Felion.Bot.Components.Evaluation;

public sealed class EvaluationButtonModule(
    IDiscordAuthorizationService authorizationService,
    IEvaluationManagementService evaluationService,
    ConfiguredDiscordGuild configuredGuild)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction(EvaluationInteractionIds.CloseConfirm)]
    public async Task<InteractionCallbackProperties> ConfirmCloseAsync(Guid periodId)
    {
        if (!TryGetDiscordUserId(out var discordUserId))
        {
            return AdministrationCommandResponses.Error("The Discord user ID is not supported.");
        }

        try
        {
            var actor = await authorizationService.RequireAdminAsync(discordUserId, CancellationToken.None);
            var period = await evaluationService.ClosePeriodAsync(
                actor.MemberId,
                periodId,
                CorrelationId(),
                actor.DiscordUserId,
                CancellationToken.None);
            return EvaluationComponentSupport.Modify(EvaluationMessageFactory.Closed(period));
        }
        catch (Exception exception) when (IsEvaluationException(exception)
            || exception is DiscordAuthorizationException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(EvaluationInteractionIds.CloseCancel)]
    public static Task<InteractionCallbackProperties> CancelCloseAsync()
    {
        return Task.FromResult<InteractionCallbackProperties>(
            EvaluationComponentSupport.Modify(
                new InteractionMessageProperties()
                    .WithContent("Đã hủy thao tác đóng evaluation.")
                    .WithFlags(MessageFlags.Ephemeral)));
    }

    private bool TryGetDiscordUserId(out long discordUserId)
    {
        if (!configuredGuild.Matches(Context.Interaction.GuildId))
        {
            discordUserId = 0;
            return false;
        }

        return EvaluationComponentSupport.TryGetDiscordUserId(Context.Interaction.User.Id, out discordUserId);
    }

    private string CorrelationId() =>
        EvaluationComponentSupport.CorrelationId(Context.Interaction.Id);

    private static bool IsEvaluationException(Exception exception) =>
        exception is EvaluationAdminAccessDeniedException
            or EvaluationPeriodNotFoundException
            or EvaluationValidationException
            or EvaluationConflictException;
}

public sealed class EvaluationSelectModule(
    IEvaluationManagementService evaluationService,
    ConfiguredDiscordGuild configuredGuild)
    : ComponentInteractionModule<StringMenuInteractionContext>
{
    [ComponentInteraction(EvaluationInteractionIds.PeerSelect)]
    public async Task<InteractionCallbackProperties> OpenPeerModalAsync(Guid periodId)
    {
        if (!TryGetDiscordUserId(out var discordUserId))
        {
            return AdministrationCommandResponses.Error("Interaction chỉ dùng trong Felion guild.");
        }

        if (!Guid.TryParse(Context.SelectedValues.SingleOrDefault(), out var targetCandidateId))
        {
            return AdministrationCommandResponses.Error("Candidate được chọn không hợp lệ.");
        }

        try
        {
            var existing = await evaluationService.GetPeerEvaluationAsync(
                discordUserId,
                periodId,
                targetCandidateId,
                CancellationToken.None);
            return EvaluationMessageFactory.PeerModal(periodId, targetCandidateId, existing);
        }
        catch (Exception exception) when (IsParticipantException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(EvaluationInteractionIds.MentorSelect)]
    public async Task<InteractionCallbackProperties> OpenMentorModalAsync(Guid periodId)
    {
        if (!TryGetDiscordUserId(out var discordUserId))
        {
            return AdministrationCommandResponses.Error("Interaction chỉ dùng trong Felion guild.");
        }

        if (!Guid.TryParse(Context.SelectedValues.SingleOrDefault(), out var targetCandidateId))
        {
            return AdministrationCommandResponses.Error("Candidate được chọn không hợp lệ.");
        }

        try
        {
            var existing = await evaluationService.GetMentorEvaluationAsync(
                discordUserId,
                periodId,
                targetCandidateId,
                CancellationToken.None);
            return EvaluationMessageFactory.MentorModal(periodId, targetCandidateId, existing);
        }
        catch (Exception exception) when (IsParticipantException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    private bool TryGetDiscordUserId(out long discordUserId)
    {
        if (!configuredGuild.Matches(Context.Interaction.GuildId))
        {
            discordUserId = 0;
            return false;
        }

        return EvaluationComponentSupport.TryGetDiscordUserId(Context.Interaction.User.Id, out discordUserId);
    }

    private static bool IsParticipantException(Exception exception) =>
        exception is EvaluationParticipantAccessDeniedException
            or EvaluationPeriodNotFoundException
            or EvaluationPeriodRequiredException
            or EvaluationValidationException
            or EvaluationConflictException;
}
