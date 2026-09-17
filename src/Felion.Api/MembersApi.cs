using Felion.Application.Discord;
using Felion.Application.Identity;
using Felion.Application.Members;
using Felion.Domain.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Felion.Api;

public static class MembersApi
{
    public const string TemporaryActorHeaderName = WebIdentityHeaders.TemporaryActorMemberId;

    public static IEndpointRouteBuilder MapFelionApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Members");
        var management = group
            .MapGroup("/members")
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);

        DiscordLinkManagementApi.Map(management);

        management.MapGet("", ListMembersAsync);
        management.MapGet("/{memberId:guid}", GetMemberAsync);
        management.MapPost("", CreateMemberAsync);
        management.MapPatch("/{memberId:guid}", UpdateMemberAsync);

        var imports = group
            .MapGroup("/imports")
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        imports.MapPost("/members", ImportMembersAsync);

        DiscordRoleMappingsApi.Map(group);
        DiscordRoleAssignmentsApi.Map(group);
        ProbationCandidatesApi.Map(group);
        ProbationTeamsApi.Map(group);
        EvaluationsApi.Map(group);
        ProbationDecisionsApi.Map(group);
        EventsApi.Map(group);
        AuthenticationApi.Map(endpoints);

        return endpoints;

        async Task<IResult> ListMembersAsync(
            HttpContext httpContext,
            IMemberManagementService service,
            int page = 1,
            int pageSize = 50,
            MemberStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ListAsync(
                    actorMemberId,
                    new ListMembersQuery(page, pageSize, status),
                    cancellationToken));
            }
            catch (Exception exception) when (exception is MemberAccessDeniedException or MemberValidationException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> GetMemberAsync(
            Guid memberId,
            HttpContext httpContext,
            IMemberManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.GetAsync(actorMemberId, memberId, cancellationToken));
            }
            catch (Exception exception) when (exception is MemberAccessDeniedException or MemberNotFoundException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> CreateMemberAsync(
            CreateMemberRequest request,
            HttpContext httpContext,
            IMemberManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            if (!Enum.TryParse<MemberPosition>(request.Position, ignoreCase: true, out var position))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Position)] = ["Position must be Admin, Core or Member."]
                });
            }

            try
            {
                var member = await service.CreateAsync(
                    actorMemberId,
                    new CreateMemberCommand(
                        request.StudentId,
                        request.FullName,
                        request.ClubEmail,
                        request.DepartmentId,
                        request.GenerationId,
                        position),
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Created($"/api/v1/members/{member.Id}", member);
            }
            catch (Exception exception) when (exception is MemberAccessDeniedException
                or MemberValidationException
                or MemberConflictException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> UpdateMemberAsync(
            Guid memberId,
            UpdateMemberRequest request,
            HttpContext httpContext,
            IMemberManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            MemberPosition? position = null;
            if (request.Position is not null)
            {
                if (!Enum.TryParse<MemberPosition>(request.Position, ignoreCase: true, out var parsedPosition))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(request.Position)] = ["Position must be Admin, Core or Member."]
                    });
                }

                position = parsedPosition;
            }

            MemberStatus? status = null;
            if (request.Status is not null)
            {
                if (!Enum.TryParse<MemberStatus>(request.Status, ignoreCase: true, out var parsedStatus))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(request.Status)] = ["Status must be Active or Inactive."]
                    });
                }

                status = parsedStatus;
            }

            try
            {
                var member = await service.UpdateAsync(
                    actorMemberId,
                    memberId,
                    new UpdateMemberCommand(
                        request.StudentId,
                        request.FullName,
                        request.ClubEmail,
                        request.DepartmentId,
                        request.GenerationId,
                        position,
                        status),
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Ok(member);
            }
            catch (Exception exception) when (exception is MemberAccessDeniedException
                or MemberNotFoundException
                or MemberValidationException
                or MemberConflictException
                or DiscordRoleGatewayException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> ImportMembersAsync(
            HttpContext httpContext,
            IMemberManagementService service,
            IMemberImportReader reader,
            IFormFile? file,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            if (file is null || file.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["A non-empty .csv or .xlsx file is required."]
                });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var readResult = await reader.ReadAsync(stream, file.FileName, cancellationToken);
                var report = await service.ImportAsync(
                    actorMemberId,
                    Path.GetFileName(file.FileName),
                    readResult,
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return report.Committed ? Results.Ok(report) : Results.BadRequest(report);
            }
            catch (Exception exception) when (exception is MemberAccessDeniedException or InvalidDataException)
            {
                return ToProblem(exception);
            }
        }

        static bool TryGetAuthenticatedActor(HttpContext httpContext, out Guid actorMemberId)
        {
            return WebIdentityPrincipal.TryGetMemberId(httpContext.User, out actorMemberId);
        }

        static string GetCorrelationId(HttpContext httpContext)
        {
            return httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
                ?? Guid.NewGuid().ToString("N");
        }

        static IResult ToProblem(Exception exception)
        {
            var statusCode = exception switch
            {
                MemberAccessDeniedException => StatusCodes.Status403Forbidden,
                MemberNotFoundException => StatusCodes.Status404NotFound,
                MemberConflictException => StatusCodes.Status409Conflict,
                DiscordRoleGatewayException => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status400BadRequest
            };

            return Results.Problem(statusCode: statusCode, detail: exception.Message);
        }
    }

    public sealed record CreateMemberRequest(
        string StudentId,
        string FullName,
        string ClubEmail,
        Guid DepartmentId,
        Guid GenerationId,
        string Position);

    public sealed record UpdateMemberRequest(
        string? StudentId = null,
        string? FullName = null,
        string? ClubEmail = null,
        Guid? DepartmentId = null,
        Guid? GenerationId = null,
        string? Position = null,
        string? Status = null);
}
