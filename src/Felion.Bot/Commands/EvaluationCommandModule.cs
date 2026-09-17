using Felion.Application.Discord;
using Felion.Application.Probation;
using Felion.Bot.Components.Evaluation;
using Felion.Bot.Configuration;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Felion.Bot.Commands;

[SlashCommand("evaluation", "Run probation evaluations")]
public sealed class EvaluationCommandModule(
    IDiscordAuthorizationService authorizationService,
    IEvaluationManagementService evaluationService,
    ConfiguredDiscordGuild configuredGuild)
    : AdministrationCommandModuleBase(authorizationService, configuredGuild)
{
    [SubSlashCommand("create", "Create and open an evaluation period")]
    public async Task<InteractionCallbackProperties> CreateAsync(
        [SlashCommandParameter(Description = "Evaluation period name")] string name)
    {
        if (!TryPrepare(out var discordUserId, out var error))
        {
            return error!;
        }

        try
        {
            var actor = await AuthorizationService.RequireAdminAsync(discordUserId, CancellationToken.None);
            var period = await evaluationService.CreatePeriodAsync(
                actor.MemberId,
                new CreateEvaluationPeriodCommand(name),
                CorrelationId(),
                actor.DiscordUserId,
                CancellationToken.None);
            return InteractionCallback.Message(EvaluationMessageFactory.Created(period));
        }
        catch (Exception exception) when (IsEvaluationException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [SubSlashCommand("current", "Show the current open evaluation")]
    public async Task<InteractionCallbackProperties> CurrentAsync()
    {
        if (!IsConfiguredGuild())
        {
            return AdministrationCommandResponses.Error(
                "This command is only available in the configured Felion guild.");
        }

        try
        {
            var status = await evaluationService.GetCurrentStatusAsync(CancellationToken.None);
            return status is null
                ? AdministrationCommandResponses.Error("Hiện không có evaluation period đang mở.")
                : InteractionCallback.Message(EvaluationMessageFactory.Current(status));
        }
        catch (Exception exception) when (IsEvaluationException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [SubSlashCommand("status", "Show evaluation progress")]
    public async Task<InteractionCallbackProperties> StatusAsync()
    {
        if (!TryPrepare(out var discordUserId, out var error))
        {
            return error!;
        }

        try
        {
            var actor = await AuthorizationService.RequireCoreOrAdminAsync(discordUserId, CancellationToken.None);
            var status = await evaluationService.GetStatusAsync(actor.MemberId, null, CancellationToken.None);
            return InteractionCallback.Message(EvaluationMessageFactory.Status(status));
        }
        catch (Exception exception) when (IsEvaluationException(exception)
            || exception is DiscordAuthorizationException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [SubSlashCommand("peer", "Evaluate candidates in your probation team")]
    public async Task<InteractionCallbackProperties> PeerAsync()
    {
        if (!TryPrepare(out var discordUserId, out var error))
        {
            return error!;
        }

        try
        {
            var targets = await evaluationService.GetPeerTargetsAsync(discordUserId, null, CancellationToken.None);
            return InteractionCallback.Message(EvaluationMessageFactory.Targets(targets, mentor: false));
        }
        catch (Exception exception) when (IsEvaluationException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [SubSlashCommand("mentor", "Evaluate probation candidates you mentor")]
    public async Task<InteractionCallbackProperties> MentorAsync()
    {
        if (!TryPrepare(out var discordUserId, out var error))
        {
            return error!;
        }

        try
        {
            var targets = await evaluationService.GetMentorTargetsAsync(discordUserId, null, CancellationToken.None);
            return InteractionCallback.Message(EvaluationMessageFactory.Targets(targets, mentor: true));
        }
        catch (Exception exception) when (IsEvaluationException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [SubSlashCommand("view", "View evaluation details for a candidate")]
    public async Task<InteractionCallbackProperties> ViewAsync(
        [SlashCommandParameter(Description = "Candidate ID")] string candidateId,
        [SlashCommandParameter(Description = "Optional evaluation period name")] string? periodName = null)
    {
        if (!TryPrepare(out var discordUserId, out var error))
        {
            return error!;
        }

        if (!Guid.TryParse(candidateId, out var parsedCandidateId))
        {
            return AdministrationCommandResponses.Error("candidateId phải là GUID hợp lệ.");
        }

        try
        {
            var actor = await AuthorizationService.RequireCoreOrAdminAsync(discordUserId, CancellationToken.None);
            var detail = await evaluationService.ViewByPeriodNameAsync(
                actor.MemberId,
                parsedCandidateId,
                periodName,
                CancellationToken.None);
            return InteractionCallback.Message(EvaluationMessageFactory.Detail(detail));
        }
        catch (Exception exception) when (IsEvaluationException(exception)
            || exception is DiscordAuthorizationException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [SubSlashCommand("summary", "Show period evaluation summary")]
    public async Task<InteractionCallbackProperties> SummaryAsync(
        [SlashCommandParameter(Description = "Evaluation period name")] string periodName,
        [SlashCommandParameter(Description = "Optional probation team name")] string? teamName = null)
    {
        if (!TryPrepare(out var discordUserId, out var error))
        {
            return error!;
        }

        try
        {
            var actor = await AuthorizationService.RequireCoreOrAdminAsync(discordUserId, CancellationToken.None);
            var summary = await evaluationService.SummaryByNameAsync(
                actor.MemberId,
                periodName,
                teamName,
                CancellationToken.None);
            return InteractionCallback.Message(EvaluationMessageFactory.Summary(summary));
        }
        catch (Exception exception) when (IsEvaluationException(exception)
            || exception is DiscordAuthorizationException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [SubSlashCommand("close", "Close an evaluation period")]
    public async Task<InteractionCallbackProperties> CloseAsync(
        [SlashCommandParameter(Description = "Optional evaluation period name")] string? periodName = null)
    {
        if (!TryPrepare(out var discordUserId, out var error))
        {
            return error!;
        }

        try
        {
            var actor = await AuthorizationService.RequireAdminAsync(discordUserId, CancellationToken.None);
            var status = string.IsNullOrWhiteSpace(periodName)
                ? await evaluationService.GetStatusAsync(actor.MemberId, null, CancellationToken.None)
                : await evaluationService.GetStatusByNameAsync(actor.MemberId, periodName, CancellationToken.None);
            return InteractionCallback.Message(EvaluationMessageFactory.CloseConfirmation(status));
        }
        catch (Exception exception) when (IsEvaluationException(exception)
            || exception is DiscordAuthorizationException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    private bool TryPrepare(out long discordUserId, out InteractionCallbackProperties? error)
    {
        if (!IsConfiguredGuild())
        {
            discordUserId = 0;
            error = AdministrationCommandResponses.Error(
                "This command is only available in the configured Felion guild.");
            return false;
        }

        if (!TryGetDiscordUserId(out discordUserId))
        {
            error = AdministrationCommandResponses.Error("The Discord user ID is not supported.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsEvaluationException(Exception exception) =>
        exception is EvaluationAdminAccessDeniedException
            or EvaluationParticipantAccessDeniedException
            or EvaluationPeriodNotFoundException
            or EvaluationPeriodNameNotFoundException
            or EvaluationPeriodNameAmbiguousException
            or EvaluationPeriodRequiredException
            or EvaluationValidationException
            or EvaluationSubmissionValidationException
            or EvaluationConflictException
            or ProbationCandidateNotFoundException
            or ProbationTeamNotFoundException
            or ProbationTeamNameNotFoundException
            or ProbationTeamNameAmbiguousException;
}
