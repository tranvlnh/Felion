using Felion.Application.Identity;
using Felion.Application.Probation;
using Felion.Domain.Probation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Felion.Api;

internal static class ProbationCandidatesApi
{
    public static void Map(IEndpointRouteBuilder group)
    {
        var candidates = group
            .MapGroup("/probation/candidates")
            .WithTags("Probation")
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);

        candidates.MapGet("", ListAsync);
        candidates.MapGet("/reference-data", GetReferenceDataAsync);
        candidates.MapGet("/mentor-options", SearchMentorsAsync);
        candidates.MapGet("/{candidateId:guid}", GetAsync);
        candidates.MapPost("", CreateAsync);
        candidates.MapPatch("/{candidateId:guid}", UpdateAsync);
        candidates.MapPut("/{candidateId:guid}/team", ChangeTeamAsync);

        static async Task<IResult> ListAsync(
            HttpContext httpContext,
            IProbationCandidateManagementService service,
            int page = 1,
            int pageSize = 50,
            string? search = null,
            Guid? departmentId = null,
            Guid? generationId = null,
            Guid? teamId = null,
            bool? hasTeam = null,
            string? status = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            ProbationCandidateStatus? parsedStatus = null;
            if (status is not null)
            {
                if (!Enum.TryParse<ProbationCandidateStatus>(status, ignoreCase: true, out var value)
                    || !Enum.IsDefined(value))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(status)] = ["Status must be Active, Passed, Failed or Archived."]
                    });
                }

                parsedStatus = value;
            }

            try
            {
                return Results.Ok(await service.ListAsync(
                    actorMemberId,
                    new ListProbationCandidatesQuery(
                        page,
                        pageSize,
                        search,
                        departmentId,
                        generationId,
                        teamId,
                        hasTeam,
                        parsedStatus),
                    cancellationToken));
            }
            catch (Exception exception) when (exception is ProbationCandidateManagementAccessDeniedException
                or ProbationCandidateValidationException)
            {
                return ToProblem(exception);
            }
        }

        static async Task<IResult> GetReferenceDataAsync(
            HttpContext httpContext,
            IProbationCandidateManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteDataAsync(
                httpContext,
                actorMemberId => service.GetReferenceDataAsync(actorMemberId, cancellationToken));
        }

        static async Task<IResult> SearchMentorsAsync(
            HttpContext httpContext,
            IProbationCandidateManagementService service,
            string? search = null,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteDataAsync(
                httpContext,
                actorMemberId => service.SearchMentorsAsync(actorMemberId, search, limit, cancellationToken));
        }

        static async Task<IResult> GetAsync(
            Guid candidateId,
            HttpContext httpContext,
            IProbationCandidateManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteDataAsync(
                httpContext,
                actorMemberId => service.GetAsync(actorMemberId, candidateId, cancellationToken));
        }

        static async Task<IResult> CreateAsync(
            CreateCandidateRequest request,
            HttpContext httpContext,
            IProbationCandidateManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                httpContext,
                async actorMemberId =>
                {
                    var candidate = await service.CreateAsync(
                        actorMemberId,
                        new CreateProbationCandidateCommand(
                            request.StudentId,
                            request.FullName,
                            request.DepartmentId,
                            request.GenerationId),
                        GetCorrelationId(httpContext),
                        cancellationToken);
                    return Results.Created($"/api/v1/probation/candidates/{candidate.Id}", candidate);
                });
        }

        static async Task<IResult> UpdateAsync(
            Guid candidateId,
            UpdateCandidateRequest request,
            HttpContext httpContext,
            IProbationCandidateManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                httpContext,
                async actorMemberId => Results.Ok(await service.UpdateAsync(
                    actorMemberId,
                    candidateId,
                    new UpdateProbationCandidateCommand(
                        request.StudentId,
                        request.FullName,
                        request.DepartmentId,
                        request.GenerationId),
                    GetCorrelationId(httpContext),
                    cancellationToken)));
        }

        static async Task<IResult> ChangeTeamAsync(
            Guid candidateId,
            ChangeCandidateTeamRequest request,
            HttpContext httpContext,
            IProbationCandidateManagementService service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(
                httpContext,
                async actorMemberId => Results.Ok(await service.ChangeTeamAsync(
                    actorMemberId,
                    candidateId,
                    request.TeamId,
                    GetCorrelationId(httpContext),
                    cancellationToken)));
        }
    }

    private static async Task<IResult> ExecuteAsync<T>(
        HttpContext httpContext,
        Func<Guid, Task<T>> action)
        where T : IResult
    {
        if (!TryGetActor(httpContext, out var actorMemberId))
        {
            return Results.Unauthorized();
        }

        try
        {
            return await action(actorMemberId);
        }
        catch (Exception exception) when (exception is ProbationCandidateManagementAccessDeniedException
            or ProbationCandidateNotFoundException
            or ProbationTeamNotFoundException
            or ProbationCandidateValidationException
            or ProbationCandidateConflictException)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> ExecuteDataAsync<T>(
        HttpContext httpContext,
        Func<Guid, Task<T>> action)
    {
        if (!TryGetActor(httpContext, out var actorMemberId))
        {
            return Results.Unauthorized();
        }

        try
        {
            return Results.Ok(await action(actorMemberId));
        }
        catch (Exception exception) when (exception is ProbationCandidateManagementAccessDeniedException
            or ProbationCandidateNotFoundException
            or ProbationTeamNotFoundException
            or ProbationCandidateValidationException
            or ProbationCandidateConflictException)
        {
            return ToProblem(exception);
        }
    }

    private static bool TryGetActor(HttpContext httpContext, out Guid actorMemberId)
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
            ProbationCandidateManagementAccessDeniedException => StatusCodes.Status403Forbidden,
            ProbationCandidateNotFoundException or ProbationTeamNotFoundException => StatusCodes.Status404NotFound,
            ProbationCandidateConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(statusCode: statusCode, detail: exception.Message);
    }

    public sealed record CreateCandidateRequest(
        string StudentId,
        string FullName,
        Guid DepartmentId,
        Guid GenerationId);

    public sealed record UpdateCandidateRequest(
        string StudentId,
        string FullName,
        Guid DepartmentId,
        Guid GenerationId);

    public sealed record ChangeCandidateTeamRequest(Guid? TeamId);
}
