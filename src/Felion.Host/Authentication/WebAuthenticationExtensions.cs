using System.Security.Claims;
using Felion.Application.Identity;
using Felion.Domain.Members;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Felion.Host.Authentication;

public static class WebAuthenticationExtensions
{
    public static IServiceCollection AddFelionWebAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "Felion.Authentication";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnValidatePrincipal = ValidateCookiePrincipalAsync;
                options.Events.OnRedirectToLogin = context => RedirectOrWriteStatusAsync(context, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => RedirectOrWriteStatusAsync(context, StatusCodes.Status403Forbidden);
            });

        var clientId = configuration["Authentication:Google:ClientId"];
        var clientSecret = configuration["Authentication:Google:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
        {
            var workspaceDomain = configuration["Authentication:Google:WorkspaceDomain"];
            services.AddAuthentication().AddGoogle(options =>
            {
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.SaveTokens = false;
                options.Scope.Add("email");
                options.Events.OnCreatingTicket = context =>
                    CreateWebMemberTicketAsync(context, workspaceDomain);
            });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(FelionAuthorizationPolicies.ActiveMember, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(FelionClaimTypes.MemberId);
                policy.RequireClaim(FelionClaimTypes.Position);
            })
            .AddPolicy(FelionAuthorizationPolicies.CoreOrAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(FelionClaimTypes.MemberId);
                policy.RequireRole(MemberPosition.Core.ToString(), MemberPosition.Admin.ToString());
            })
            .AddPolicy(FelionAuthorizationPolicies.AdminOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(FelionClaimTypes.MemberId);
                policy.RequireRole(MemberPosition.Admin.ToString());
            });

        return services;
    }

    private static async Task CreateWebMemberTicketAsync(
        OAuthCreatingTicketContext context,
        string? workspaceDomain)
    {
        var email = context.Identity?.FindFirst(ClaimTypes.Email)?.Value
            ?? context.Identity?.FindFirst("email")?.Value;
        if (!IsWorkspaceEmail(email, workspaceDomain))
        {
            context.Fail("The Google account is outside the configured Workspace domain.");
            return;
        }

        var identityService = context.HttpContext.RequestServices.GetRequiredService<IWebIdentityService>();
        var member = await identityService.ResolveActiveMemberByEmailAsync(
            email,
            context.HttpContext.RequestAborted);
        if (member is null || context.Identity is null)
        {
            context.Fail("The Google account is not linked to an active Felion member.");
            return;
        }

        WebIdentityPrincipal.ReplaceMemberClaims(context.Identity, member);
    }

    private static async Task ValidateCookiePrincipalAsync(
        CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var email = principal?.FindFirstValue(ClaimTypes.Email);
        var identityService = context.HttpContext.RequestServices.GetRequiredService<IWebIdentityService>();
        var member = await identityService.ResolveActiveMemberByEmailAsync(
            email,
            context.HttpContext.RequestAborted);

        if (member is null
            || principal is null
            || !WebIdentityPrincipal.TryGetMemberId(principal, out var memberId)
            || member.MemberId != memberId)
        {
            context.RejectPrincipal();
            return;
        }

        var identity = principal!.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated);
        if (identity is not null)
        {
            WebIdentityPrincipal.ReplaceMemberClaims(identity, member);
        }
    }

    private static Task RedirectOrWriteStatusAsync<TOptions>(
        RedirectContext<TOptions> context,
        int statusCode)
        where TOptions : AuthenticationSchemeOptions
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    private static bool IsWorkspaceEmail(string? email, string? workspaceDomain)
    {
        if (string.IsNullOrWhiteSpace(workspaceDomain))
        {
            return true;
        }

        var atIndex = email?.LastIndexOf('@') ?? -1;
        var normalizedDomain = workspaceDomain.Trim().TrimStart('@');
        return atIndex > 0
            && !string.IsNullOrWhiteSpace(normalizedDomain)
            && string.Equals(
                email![(atIndex + 1)..],
                normalizedDomain,
                StringComparison.OrdinalIgnoreCase);
    }
}
