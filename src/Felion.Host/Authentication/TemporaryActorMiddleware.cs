using Felion.Application.Identity;

namespace Felion.Host.Authentication;

public sealed class TemporaryActorMiddleware(
    RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IWebIdentityService identityService)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true
            && Guid.TryParse(
                httpContext.Request.Headers[WebIdentityHeaders.TemporaryActorMemberId].FirstOrDefault(),
                out var actorMemberId)
            && actorMemberId != Guid.Empty)
        {
            var member = await identityService.ResolveActiveMemberByIdAsync(
                actorMemberId,
                httpContext.RequestAborted);
            if (member is not null)
            {
                httpContext.User = WebIdentityPrincipal.Create(member, "Felion.Temporary");
            }
        }

        await next(httpContext);
    }
}
