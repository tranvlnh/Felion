using Felion.Application.Identity;
using Felion.Application.Probation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Felion.Api;

internal static class ProbationTeamsApi
{
    public static void Map(IEndpointRouteBuilder group)
    {
        var teams = group
            .MapGroup("/probation/teams")
            .WithTags("Probation")
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);

        teams.MapGet("", ListAsync);
        teams.MapGet("/{teamId:guid}", GetAsync);
        teams.MapPost("", CreateAsync);
        teams.MapPatch("/{teamId:guid}", UpdateAsync);
        teams.MapPut("/{teamId:guid}/candidates/{candidateId:guid}", AssignCandidateAsync);
        teams.MapDelete("/{teamId:guid}/candidates/{candidateId:guid}", RemoveCandidateAsync);
        teams.MapPut("/{teamId:guid}/mentors/{memberId:guid}", AssignMentorAsync);
        teams.MapDelete("/{teamId:guid}/mentors/{memberId:guid}", RemoveMentorAsync);

        async Task<IResult> ListAsync(
            HttpContext httpContext,
            IProbationTeamManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ListAsync(actorMemberId, cancellationToken));
            }
            catch (ProbationTeamAccessDeniedException exception)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> GetAsync(
            Guid teamId,
            HttpContext httpContext,
            IProbationTeamManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.GetAsync(actorMemberId, teamId, cancellationToken));
            }
            catch (Exception exception) when (exception is ProbationTeamAccessDeniedException
                or ProbationTeamNotFoundException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> CreateAsync(
            CreateTeamRequest request,
            HttpContext httpContext,
            IProbationTeamManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var team = await service.CreateAsync(
                    actorMemberId,
                    new CreateProbationTeamCommand(request.Name),
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Created($"/api/v1/probation/teams/{team.Id}", team);
            }
            catch (Exception exception) when (exception is ProbationTeamAccessDeniedException
                or ProbationTeamValidationException
                or ProbationTeamConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> UpdateAsync(
            Guid teamId,
            UpdateTeamRequest request,
            HttpContext httpContext,
            IProbationTeamManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.UpdateAsync(
                    actorMemberId,
                    teamId,
                    new UpdateProbationTeamCommand(request.Name, request.IsActive),
                    GetCorrelationId(httpContext),
                    cancellationToken));
            }
            catch (Exception exception) when (exception is ProbationTeamAccessDeniedException
                or ProbationTeamNotFoundException
                or ProbationTeamValidationException
                or ProbationTeamConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> AssignCandidateAsync(
            Guid teamId,
            Guid candidateId,
            HttpContext httpContext,
            IProbationTeamManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAssignmentAsync(
                () => service.AssignCandidateAsync(
                    GetActor(httpContext),
                    teamId,
                    candidateId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> RemoveCandidateAsync(
            Guid teamId,
            Guid candidateId,
            HttpContext httpContext,
            IProbationTeamManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAssignmentAsync(
                () => service.RemoveCandidateAsync(
                    GetActor(httpContext),
                    teamId,
                    candidateId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> AssignMentorAsync(
            Guid teamId,
            Guid memberId,
            HttpContext httpContext,
            IProbationTeamManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAssignmentAsync(
                () => service.AssignMentorAsync(
                    GetActor(httpContext),
                    teamId,
                    memberId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        async Task<IResult> RemoveMentorAsync(
            Guid teamId,
            Guid memberId,
            HttpContext httpContext,
            IProbationTeamManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAssignmentAsync(
                () => service.RemoveMentorAsync(
                    GetActor(httpContext),
                    teamId,
                    memberId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
        }

        static Guid GetActor(HttpContext httpContext)
        {
            return WebIdentityPrincipal.TryGetMemberId(httpContext.User, out var actorMemberId)
                ? actorMemberId
                : throw new UnauthorizedAccessException();
        }

        static async Task<IResult> ExecuteAssignmentAsync(Func<Task<ProbationTeamDto>> action)
        {
            try
            {
                return Results.Ok(await action());
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception exception) when (exception is ProbationTeamAccessDeniedException
                or ProbationTeamNotFoundException
                or ProbationCandidateNotFoundException
                or ProbationMentorNotFoundException
                or ProbationTeamValidationException
                or ProbationTeamConflictException)
            {
                return ToProblem(exception);
            }
        }
    }

    private static bool TryGetAuthenticatedActor(HttpContext httpContext, out Guid actorMemberId)
    {
        return WebIdentityPrincipal.TryGetMemberId(httpContext.User, out actorMemberId);
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        return httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
    }

    private static IResult ToProblem(Exception exception)
    {
        var statusCode = exception switch
        {
            ProbationTeamAccessDeniedException => StatusCodes.Status403Forbidden,
            ProbationTeamNotFoundException or ProbationCandidateNotFoundException or ProbationMentorNotFoundException
                => StatusCodes.Status404NotFound,
            ProbationTeamConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(statusCode: statusCode, detail: exception.Message);
    }

    public sealed record CreateTeamRequest(string Name);

    public sealed record UpdateTeamRequest(string? Name = null, bool? IsActive = null);
}
