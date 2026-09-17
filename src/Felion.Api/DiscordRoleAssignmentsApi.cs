using Felion.Application.Discord;
using Felion.Application.Identity;
using Felion.Domain.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;

namespace Felion.Api;

internal static class DiscordRoleAssignmentsApi
{
    public static void Map(IEndpointRouteBuilder group)
    {
        var assignments = group
            .MapGroup("/discord/role-assignments")
            .WithTags("Discord")
            .RequireAuthorization(FelionAuthorizationPolicies.AdminOnly);
        assignments.MapGet("", ListAsync);
        assignments.MapGet("/roles", ListRolesAsync);
        assignments.MapPut("/{subjectType}/{subjectId:guid}", ReplaceAsync);

        async Task<IResult> ListAsync(
            HttpContext httpContext,
            IDiscordRoleAssignmentService service,
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
            catch (DiscordRoleAssignmentAccessDeniedException exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: exception.Message);
            }
        }

        async Task<IResult> ListRolesAsync(
            HttpContext httpContext,
            IDiscordRoleAssignmentService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ListRolesAsync(actorMemberId, cancellationToken));
            }
            catch (Exception exception) when (exception is DiscordRoleAssignmentAccessDeniedException
                or DiscordRoleUnavailableException
                or DiscordRoleGatewayException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> ReplaceAsync(
            string subjectType,
            Guid subjectId,
            UpdateRoleAssignmentsRequest request,
            HttpContext httpContext,
            IDiscordRoleAssignmentService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            if (!Enum.TryParse<DiscordIdentitySubjectType>(subjectType, ignoreCase: true, out var parsedSubjectType)
                || !Enum.IsDefined(parsedSubjectType))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(subjectType)] = ["Subject type must be Member or Probation."]
                });
            }

            if (request.DiscordRoleIds is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.DiscordRoleIds)] = ["DiscordRoleIds is required."]
                });
            }

            var discordRoleIds = new List<long>(request.DiscordRoleIds.Count);
            foreach (var roleId in request.DiscordRoleIds)
            {
                if (!long.TryParse(roleId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRoleId))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(request.DiscordRoleIds)] = ["Each Discord role ID must be a positive signed snowflake."]
                    });
                }

                discordRoleIds.Add(parsedRoleId);
            }

            try
            {
                return Results.Ok(await service.ReplaceAsync(
                    actorMemberId,
                    parsedSubjectType,
                    subjectId,
                    new UpdateDiscordRoleAssignmentsCommand(discordRoleIds),
                    GetCorrelationId(httpContext),
                    cancellationToken));
            }
            catch (Exception exception) when (exception is DiscordRoleAssignmentAccessDeniedException
                or DiscordRoleAssignmentNotFoundException
                or DiscordRoleAssignmentValidationException
                or DiscordRoleAssignmentConflictException
                or DiscordRoleUnavailableException
                or DiscordRoleGatewayException)
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
            DiscordRoleAssignmentAccessDeniedException => StatusCodes.Status403Forbidden,
            DiscordRoleAssignmentNotFoundException => StatusCodes.Status404NotFound,
            DiscordRoleAssignmentConflictException => StatusCodes.Status409Conflict,
            DiscordRoleUnavailableException => StatusCodes.Status503ServiceUnavailable,
            DiscordRoleGatewayException => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(statusCode: statusCode, detail: exception.Message);
    }

    public sealed record UpdateRoleAssignmentsRequest(IReadOnlyList<string>? DiscordRoleIds);
}
