using Felion.Application.Hardening;
using Felion.Application.Identity;
using Felion.Application.Probation;
using Felion.Domain.Evaluation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Felion.Api;

internal static class EvaluationsApi
{
    public static void Map(IEndpointRouteBuilder group)
    {
        var periods = group
            .MapGroup("/probation/evaluation-periods")
            .WithTags("Probation Evaluations")
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);

        periods.MapGet("", ListPeriodsAsync);
        periods.MapPost("", CreatePeriodAsync);
        periods.MapPost("/{periodId:guid}/open", OpenPeriodAsync);
        periods.MapPost("/{periodId:guid}/close", ClosePeriodAsync);

        var forms = group
            .MapGroup("/probation/evaluation-forms")
            .WithTags("Probation Evaluations")
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);

        forms.MapPost("", CreateFormAsync);
        forms.MapPatch("/{formId:guid}", UpdateFormAsync);

        var evaluations = group
            .MapGroup("/probation/evaluations")
            .WithTags("Probation Evaluations");
        evaluations
            .MapPost("/mentor", SubmitMentorAsync)
            .RequireAuthorization(FelionAuthorizationPolicies.ActiveMember)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        evaluations
            .MapGet("/results", ListResultsAsync)
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);

        async Task<IResult> ListPeriodsAsync(
            HttpContext httpContext,
            IEvaluationManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ListPeriodsAsync(actorMemberId, cancellationToken));
            }
            catch (EvaluationAccessDeniedException exception)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> CreatePeriodAsync(
            CreatePeriodRequest request,
            HttpContext httpContext,
            IEvaluationManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var period = await service.CreatePeriodAsync(
                    actorMemberId,
                    new CreateEvaluationPeriodCommand(request.Name, request.StartsAt, request.EndsAt),
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Created($"/api/v1/probation/evaluation-periods/{period.Id}", period);
            }
            catch (Exception exception) when (exception is EvaluationAccessDeniedException
                or EvaluationValidationException
                or EvaluationConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> OpenPeriodAsync(
            Guid periodId,
            HttpContext httpContext,
            IEvaluationManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecutePeriodActionAsync(
                () => service.OpenPeriodAsync(
                    GetActor(httpContext),
                    periodId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> ClosePeriodAsync(
            Guid periodId,
            HttpContext httpContext,
            IEvaluationManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecutePeriodActionAsync(
                () => service.ClosePeriodAsync(
                    GetActor(httpContext),
                    periodId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> CreateFormAsync(
            CreateFormRequest request,
            HttpContext httpContext,
            IEvaluationManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            if (!TryParseReviewerType(request.ReviewerType, out var reviewerType))
            {
                return InvalidEnumProblem(nameof(request.ReviewerType), "ReviewerType must be Peer or Mentor.");
            }

            if (!TryParseQuestions(request.Questions, out var questions, out var validationProblem))
            {
                return validationProblem!;
            }

            try
            {
                var form = await service.CreateFormAsync(
                    actorMemberId,
                    new CreateEvaluationFormCommand(
                        request.PeriodId,
                        request.Name,
                        reviewerType,
                        questions),
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Created($"/api/v1/probation/evaluation-forms/{form.Id}", form);
            }
            catch (Exception exception) when (exception is EvaluationAccessDeniedException
                or EvaluationPeriodNotFoundException
                or EvaluationValidationException
                or EvaluationConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> UpdateFormAsync(
            Guid formId,
            UpdateFormRequest request,
            HttpContext httpContext,
            IEvaluationManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            IReadOnlyList<EvaluationQuestionCommand>? questions = null;
            if (request.Questions is not null
                && !TryParseQuestions(request.Questions, out questions, out var validationProblem))
            {
                return validationProblem!;
            }

            try
            {
                return Results.Ok(await service.UpdateFormAsync(
                    actorMemberId,
                    formId,
                    new UpdateEvaluationFormCommand(request.Name, request.IsActive, questions),
                    GetCorrelationId(httpContext),
                    cancellationToken));
            }
            catch (Exception exception) when (exception is EvaluationAccessDeniedException
                or EvaluationFormNotFoundException
                or EvaluationValidationException
                or EvaluationConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> SubmitMentorAsync(
            SubmitEvaluationRequest request,
            HttpContext httpContext,
            IEvaluationManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var reviewerMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var receipt = await service.SubmitMentorEvaluationAsync(
                    reviewerMemberId,
                    new SubmitEvaluationCommand(
                        request.FormId,
                        request.TargetCandidateId,
                        request.Answers?.Select(answer => new EvaluationAnswerCommand(
                            answer.QuestionId,
                            answer.ScoreValue,
                            answer.TextValue)).ToArray() ?? []),
                    cancellationToken);
                return Results.Ok(receipt);
            }
            catch (RateLimitExceededException exception)
            {
                return ToRateLimitProblem(httpContext, exception);
            }
            catch (Exception exception) when (exception is EvaluationFormNotFoundException
                or EvaluationPeriodNotFoundException
                or ProbationCandidateNotFoundException
                or EvaluationSubmissionValidationException
                or EvaluationConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> ListResultsAsync(
            HttpContext httpContext,
            IEvaluationManagementService service,
            Guid? periodId = null,
            Guid? formId = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ListResultsAsync(
                    actorMemberId,
                    periodId,
                    formId,
                    cancellationToken));
            }
            catch (EvaluationAccessDeniedException exception)
            {
                return ToProblem(exception);
            }
        }

        static async Task<IResult> ExecutePeriodActionAsync(Func<Task<EvaluationPeriodDto>> action)
        {
            try
            {
                return Results.Ok(await action());
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception exception) when (exception is EvaluationAccessDeniedException
                or EvaluationPeriodNotFoundException
                or EvaluationValidationException
                or EvaluationConflictException)
            {
                return ToProblem(exception);
            }
        }
    }

    private static IResult ToRateLimitProblem(
        HttpContext httpContext,
        RateLimitExceededException exception)
    {
        var retryAfter = exception.RetryAfter is { } duration && duration > TimeSpan.Zero
            ? Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))
            : 60;
        httpContext.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            detail: exception.Message);
    }

    private static bool TryGetAuthenticatedActor(HttpContext httpContext, out Guid actorMemberId)
    {
        return WebIdentityPrincipal.TryGetMemberId(httpContext.User, out actorMemberId);
    }

    private static Guid GetActor(HttpContext httpContext)
    {
        return WebIdentityPrincipal.TryGetMemberId(httpContext.User, out var actorMemberId)
            ? actorMemberId
            : throw new UnauthorizedAccessException();
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        return httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
    }

    private static bool TryParseReviewerType(
        string value,
        out EvaluationReviewerType reviewerType)
    {
        return Enum.TryParse(value, ignoreCase: true, out reviewerType)
            && Enum.IsDefined(reviewerType);
    }

    private static bool TryParseQuestions(
        IReadOnlyList<QuestionRequest>? requests,
        out IReadOnlyList<EvaluationQuestionCommand> questions,
        out IResult? validationProblem)
    {
        if (requests is null)
        {
            questions = [];
            validationProblem = Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(CreateFormRequest.Questions)] = ["At least one question is required."]
            });
            return false;
        }

        var parsed = new List<EvaluationQuestionCommand>(requests.Count);
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            if (!Enum.TryParse(request.Type, ignoreCase: true, out EvaluationQuestionType type)
                || !Enum.IsDefined(type))
            {
                questions = [];
                validationProblem = Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [$"Questions[{index}].Type"] = ["Question type must be Score or Text."]
                });
                return false;
            }

            parsed.Add(new EvaluationQuestionCommand(
                request.Order,
                request.Prompt,
                type,
                request.IsRequired,
                request.ScoreMin,
                request.ScoreMax,
                request.TextMaxLength,
                request.Id));
        }

        questions = parsed;
        validationProblem = null;
        return true;
    }

    private static IResult InvalidEnumProblem(string field, string detail)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [detail]
        });
    }

    private static IResult ToProblem(Exception exception)
    {
        var statusCode = exception switch
        {
            EvaluationAccessDeniedException => StatusCodes.Status403Forbidden,
            EvaluationPeriodNotFoundException
                or EvaluationFormNotFoundException
                or ProbationCandidateNotFoundException => StatusCodes.Status404NotFound,
            EvaluationConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(statusCode: statusCode, detail: exception.Message);
    }

    public sealed record CreatePeriodRequest(
        string Name,
        DateTimeOffset? StartsAt = null,
        DateTimeOffset? EndsAt = null);

    public sealed record CreateFormRequest(
        Guid PeriodId,
        string Name,
        string ReviewerType,
        IReadOnlyList<QuestionRequest>? Questions);

    public sealed record UpdateFormRequest(
        string? Name = null,
        bool? IsActive = null,
        IReadOnlyList<QuestionRequest>? Questions = null);

    public sealed record SubmitEvaluationRequest(
        Guid FormId,
        Guid TargetCandidateId,
        IReadOnlyList<AnswerRequest>? Answers = null);

    public sealed record AnswerRequest(
        Guid QuestionId,
        decimal? ScoreValue = null,
        string? TextValue = null);

    public sealed record QuestionRequest(
        int Order,
        string Prompt,
        string Type,
        bool IsRequired,
        decimal? ScoreMin = null,
        decimal? ScoreMax = null,
        int? TextMaxLength = null,
        Guid? Id = null);
}
