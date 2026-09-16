using System.Security.Claims;
using Felion.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Felion.Api;

internal static class AuthenticationApi
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.MapGet("/login", (Delegate)LoginAsync);
        group.MapGet("/me", GetCurrentMember).RequireAuthorization(FelionAuthorizationPolicies.ActiveMember);
        group.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization(FelionAuthorizationPolicies.ActiveMember);
    }

    private static async Task<IResult> LoginAsync(
        HttpContext httpContext,
        IAuthenticationSchemeProvider schemeProvider,
        string? returnUrl = null)
    {
        if (returnUrl is not null && !IsLocalReturnUrl(returnUrl))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(returnUrl)] = ["returnUrl must be a local application path."]
            });
        }

        if (await schemeProvider.GetSchemeAsync("Google") is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Google Workspace authentication is not configured.");
        }

        return Results.Challenge(
            new AuthenticationProperties
            {
                RedirectUri = string.IsNullOrWhiteSpace(returnUrl)
                    ? "/api/v1/auth/me"
                    : returnUrl
            },
            ["Google"]);
    }

    private static IResult GetCurrentMember(HttpContext httpContext)
    {
        if (!WebIdentityPrincipal.TryGetMemberId(httpContext.User, out var memberId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new CurrentMemberResponse(
            memberId,
            httpContext.User.FindFirstValue(ClaimTypes.Name)
                ?? httpContext.User.Identity?.Name
                ?? string.Empty,
            httpContext.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            httpContext.User.FindFirstValue(FelionClaimTypes.Position) ?? string.Empty));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static bool IsLocalReturnUrl(string returnUrl)
    {
        return returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
    }

    private sealed record CurrentMemberResponse(
        Guid MemberId,
        string FullName,
        string ClubEmail,
        string Position);
}
