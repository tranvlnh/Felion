using Felion.Application.Identity;
using Felion.Application.Probation;
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
        periods.MapPost("/{periodId:guid}/close", ClosePeriodAsync);

        var evaluations = group
            .MapGroup("/probation/evaluations")
            .WithTags("Probation Evaluations")
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);

        evaluations.MapGet("/status", GetStatusAsync);
        evaluations.MapGet("/summary/{periodId:guid}", GetSummaryAsync);
        evaluations.MapGet("/{candidateId:guid}", GetDetailAsync);

        async Task<IResult> ListPeriodsAsync(
            HttpContext httpContext,
            IEvaluationManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ListPeriodsAsync(actorMemberId, cancellationToken));
            }
            catch (Exception exception) when (IsEvaluationException(exception))
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
            if (!TryGetActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var period = await service.CreatePeriodAsync(
                    actorMemberId,
                    new CreateEvaluationPeriodCommand(request.Name),
                    GetCorrelationId(httpContext),
                    actorDiscordUserId: null,
                    cancellationToken: cancellationToken);
                return Results.Created($"/api/v1/probation/evaluation-periods/{period.Id}", period);
            }
            catch (Exception exception) when (IsEvaluationException(exception))
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> ClosePeriodAsync(
            Guid periodId,
            HttpContext httpContext,
            IEvaluationManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ClosePeriodAsync(
                    actorMemberId,
                    periodId,
                    GetCorrelationId(httpContext),
                    actorDiscordUserId: null,
                    cancellationToken: cancellationToken));
            }
            catch (Exception exception) when (IsEvaluationException(exception))
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> GetStatusAsync(
            HttpContext httpContext,
            IEvaluationManagementService service,
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.GetStatusAsync(actorMemberId, periodId, cancellationToken));
            }
            catch (Exception exception) when (IsEvaluationException(exception))
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> GetSummaryAsync(
            Guid periodId,
            HttpContext httpContext,
            IEvaluationManagementService service,
            Guid? teamId = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.SummaryAsync(actorMemberId, periodId, teamId, cancellationToken));
            }
            catch (Exception exception) when (IsEvaluationException(exception))
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> GetDetailAsync(
            Guid candidateId,
            HttpContext httpContext,
            IEvaluationManagementService service,
            Guid? periodId = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ViewAsync(actorMemberId, candidateId, periodId, cancellationToken));
            }
            catch (Exception exception) when (IsEvaluationException(exception))
            {
                return ToProblem(exception);
            }
        }
    }

    private static bool TryGetActor(HttpContext httpContext, out Guid actorMemberId) =>
        WebIdentityPrincipal.TryGetMemberId(httpContext.User, out actorMemberId);

    private static string GetCorrelationId(HttpContext httpContext) =>
        httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");

    private static bool IsEvaluationException(Exception exception) =>
        exception is EvaluationAccessDeniedException
            or EvaluationAdminAccessDeniedException
            or EvaluationPeriodNotFoundException
            or EvaluationPeriodRequiredException
            or EvaluationValidationException
            or EvaluationConflictException
            or ProbationCandidateNotFoundException
            or ProbationTeamNotFoundException;

    private static IResult ToProblem(Exception exception)
    {
        var statusCode = exception switch
        {
            EvaluationAccessDeniedException
                or EvaluationAdminAccessDeniedException => StatusCodes.Status403Forbidden,
            EvaluationPeriodNotFoundException
                or ProbationCandidateNotFoundException
                or ProbationTeamNotFoundException => StatusCodes.Status404NotFound,
            EvaluationConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(statusCode: statusCode, detail: exception.Message);
    }

    public sealed record CreatePeriodRequest(string Name);
}
