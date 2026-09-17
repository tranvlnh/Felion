using Felion.Application.Hardening;
using Felion.Application.Probation;
using Felion.Bot.Commands;
using Felion.Bot.Configuration;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Felion.Bot.Components.Evaluation;

public sealed class EvaluationModalModule(
    IEvaluationManagementService evaluationService,
    ConfiguredDiscordGuild configuredGuild)
    : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction(EvaluationInteractionIds.PeerModal)]
    public async Task<InteractionCallbackProperties> SubmitPeerAsync(Guid periodId, Guid targetCandidateId)
    {
        if (!TryGetDiscordUserId(out var discordUserId))
        {
            return AdministrationCommandResponses.Error("Interaction chỉ dùng trong Felion guild.");
        }

        if (!TryParseScore("Đóng góp", GetTextInput("contribution"), 1, 5, out var contribution, out var error)
            || !TryParseScore("Giao tiếp", GetTextInput("communication"), 1, 5, out var communication, out error)
            || !TryParseScore("Thái độ", GetTextInput("attitude"), 1, 5, out var attitude, out error))
        {
            return AdministrationCommandResponses.Error(error!);
        }

        try
        {
            var receipt = await evaluationService.SubmitPeerEvaluationAsync(
                discordUserId,
                new SubmitPeerEvaluationCommand(
                    periodId,
                    targetCandidateId,
                    contribution,
                    communication,
                    attitude,
                    GetTextInput("note")),
                EvaluationComponentSupport.CorrelationId(Context.Interaction.Id),
                CancellationToken.None);
            return InteractionCallback.Message(EvaluationMessageFactory.Submitted("Peer", receipt));
        }
        catch (Exception exception) when (IsEvaluationException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
        catch (RateLimitExceededException exception)
        {
            return AdministrationCommandResponses.Error(
                $"Bạn thao tác quá nhanh. Thử lại sau {FormatRetryAfter(exception.RetryAfter)}.");
        }
    }

    [ComponentInteraction(EvaluationInteractionIds.MentorModal)]
    public async Task<InteractionCallbackProperties> SubmitMentorAsync(Guid periodId, Guid targetCandidateId)
    {
        if (!TryGetDiscordUserId(out var discordUserId))
        {
            return AdministrationCommandResponses.Error("Interaction chỉ dùng trong Felion guild.");
        }

        if (!TryParseScore("Tác phong", GetTextInput("attendance"), 1, 10, out var attendance, out var error)
            || !TryParseScore("Tiến độ công việc", GetTextInput("task-completion"), 1, 10, out var taskCompletion, out error)
            || !TryParseScore("Tinh thần học hỏi", GetTextInput("learning-initiative"), 1, 10, out var learningInitiative, out error))
        {
            return AdministrationCommandResponses.Error(error!);
        }

        try
        {
            var receipt = await evaluationService.SubmitMentorEvaluationAsync(
                discordUserId,
                new SubmitMentorEvaluationCommand(
                    periodId,
                    targetCandidateId,
                    attendance,
                    taskCompletion,
                    learningInitiative,
                    GetTextInput("note")),
                EvaluationComponentSupport.CorrelationId(Context.Interaction.Id),
                CancellationToken.None);
            return InteractionCallback.Message(EvaluationMessageFactory.Submitted("Mentor", receipt));
        }
        catch (Exception exception) when (IsEvaluationException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
        catch (RateLimitExceededException exception)
        {
            return AdministrationCommandResponses.Error(
                $"Bạn thao tác quá nhanh. Thử lại sau {FormatRetryAfter(exception.RetryAfter)}.");
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

    private string? GetTextInput(string customId) =>
        Context.Components
            .OfType<Label>()
            .Select(label => label.Component)
            .OfType<TextInput>()
            .SingleOrDefault(input => input.CustomId == customId)
            ?.Value;

    private static bool TryParseScore(
        string displayName,
        string? value,
        int minimum,
        int maximum,
        out int score,
        out string? error)
    {
        if (!int.TryParse(value, out score) || score < minimum || score > maximum)
        {
            error = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Điểm \"{0}\" phải nằm trong khoảng {1}-{2}.",
                displayName,
                minimum,
                maximum);
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsEvaluationException(Exception exception) =>
        exception is EvaluationParticipantAccessDeniedException
            or EvaluationPeriodNotFoundException
            or EvaluationPeriodRequiredException
            or EvaluationSubmissionValidationException
            or EvaluationValidationException
            or EvaluationConflictException;

    private static string FormatRetryAfter(TimeSpan? retryAfter)
    {
        if (retryAfter is null || retryAfter <= TimeSpan.Zero)
        {
            return "một lúc";
        }

        return retryAfter.Value.TotalMinutes >= 1
            ? $"{Math.Ceiling(retryAfter.Value.TotalMinutes)} phút"
            : $"{Math.Ceiling(retryAfter.Value.TotalSeconds)} giây";
    }
}
