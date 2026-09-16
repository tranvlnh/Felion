using Felion.Application.Discord;
using Felion.Application.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Felion.Api;

internal static class DiscordLinkManagementApi
{
    public static void Map(IEndpointRouteBuilder members)
    {
        members.MapPost("/{memberId:guid}/unlink-discord", UnlinkAsync)
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        members.MapPost("/{memberId:guid}/relink-discord", RelinkAsync)
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);
        members.MapPost("/{memberId:guid}/sync-discord-roles", ForceSyncAsync)
            .RequireAuthorization(FelionAuthorizationPolicies.CoreOrAdmin);

        async Task<IResult> UnlinkAsync(
            Guid memberId,
            HttpContext httpContext,
            IDiscordLinkManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.UnlinkAsync(
                    actorMemberId,
                    memberId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
            }
            catch (Exception exception) when (IsHandled(exception))
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> RelinkAsync(
            Guid memberId,
            RelinkRequest request,
            HttpContext httpContext,
            IDiscordLinkManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.RelinkAsync(
                    actorMemberId,
                    memberId,
                    new RelinkDiscordCommand(request.DiscordUserId),
                    GetCorrelationId(httpContext),
                    cancellationToken));
            }
            catch (Exception exception) when (IsHandled(exception))
            {
                return ToProblem(exception);
            }
        }

        async Task<IResult> ForceSyncAsync(
            Guid memberId,
            HttpContext httpContext,
            IDiscordLinkManagementService service,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetAuthenticatedActor(httpContext, out var actorMemberId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await service.ForceSyncAsync(
                    actorMemberId,
                    memberId,
                    GetCorrelationId(httpContext),
                    cancellationToken));
            }
            catch (Exception exception) when (IsHandled(exception))
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

    private static bool IsHandled(Exception exception)
    {
        return exception is DiscordLinkManagementAccessDeniedException
            or DiscordLinkNotFoundException
            or DiscordLinkValidationException
            or DiscordLinkConflictException;
    }

    private static IResult ToProblem(Exception exception)
    {
        var statusCode = exception switch
        {
            DiscordLinkManagementAccessDeniedException => StatusCodes.Status403Forbidden,
            DiscordLinkNotFoundException => StatusCodes.Status404NotFound,
            DiscordLinkConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(statusCode: statusCode, detail: exception.Message);
    }

    public sealed record RelinkRequest(long DiscordUserId);
}
