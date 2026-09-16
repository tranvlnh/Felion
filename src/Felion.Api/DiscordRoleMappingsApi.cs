using Felion.Application.Discord;
using Felion.Application.Identity;
using Felion.Domain.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Felion.Api;

internal static class DiscordRoleMappingsApi
{
    public static void Map(IEndpointRouteBuilder group)
    {
        var mappings = group
            .MapGroup("/discord/role-mappings")
            .WithTags("Discord")
            .RequireAuthorization(FelionAuthorizationPolicies.AdminOnly);
        mappings.MapGet("", ListAsync);
        mappings.MapPut("", UpsertAsync);

        var roles = group
            .MapGroup("/discord/roles")
            .WithTags("Discord")
            .RequireAuthorization(FelionAuthorizationPolicies.AdminOnly);
        roles.MapPost("", CreateRoleAsync);

        async Task<IResult> ListAsync(
            HttpContext httpContext,
            IDiscordRoleMappingService service,
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
            catch (DiscordRoleMappingAccessDeniedException exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: exception.Message);
            }
        }

        async Task<IResult> CreateRoleAsync(
            RoleCreateRequest request,
            HttpContext httpContext,
            IDiscordRoleManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await service.CreateAsync(
                    actorMemberId,
                    new CreateDiscordRoleCommand(request.Name),
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Ok(result);
            }
            catch (Exception exception) when (exception is DiscordRoleMappingAccessDeniedException
                or DiscordRoleManagementValidationException
                or DiscordRoleGatewayException)
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> UpsertAsync(
            RoleMappingRequest request,
            HttpContext httpContext,
            IDiscordRoleMappingService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            if (!Enum.TryParse<DiscordRoleMappingKind>(request.Kind, ignoreCase: true, out var kind)
                || !Enum.IsDefined(kind))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Kind)] = ["Kind must be Position, Probation, Department, Generation or ProbationTeam."]
                });
            }

            try
            {
                var result = await service.UpsertAsync(
                    actorMemberId,
                    new UpsertDiscordRoleMappingCommand(
                        kind,
                        request.SubjectKey,
                        request.DiscordRoleId,
                        request.RoleNameSnapshot),
                    GetCorrelationId(httpContext),
                    cancellationToken);
                return Results.Ok(result);
            }
            catch (Exception exception) when (exception is DiscordRoleMappingAccessDeniedException
                or DiscordRoleMappingValidationException
                or DiscordRoleMappingConflictException
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
            DiscordRoleMappingAccessDeniedException => StatusCodes.Status403Forbidden,
            DiscordRoleNotFoundException => StatusCodes.Status404NotFound,
            DiscordRoleUnavailableException => StatusCodes.Status503ServiceUnavailable,
            DiscordRoleGatewayException => StatusCodes.Status502BadGateway,
            DiscordRoleMappingConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(statusCode: statusCode, detail: exception.Message);
    }

    public sealed record RoleMappingRequest(
        string Kind,
        string SubjectKey,
        long DiscordRoleId,
        string RoleNameSnapshot);

    public sealed record RoleCreateRequest(string Name);
}
