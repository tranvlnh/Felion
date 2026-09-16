using System.Security.Claims;
using Felion.Domain.Members;

namespace Felion.Application.Identity;

public static class FelionClaimTypes
{
    public const string MemberId = "felion/member-id";

    public const string Position = "felion/position";
}

public static class FelionAuthorizationPolicies
{
    public const string ActiveMember = "ActiveMember";

    public const string CoreOrAdmin = "CoreOrAdmin";

    public const string AdminOnly = "AdminOnly";
}

public static class WebIdentityHeaders
{
    public const string TemporaryActorMemberId = "X-Felion-Actor-Member-Id";
}

public sealed record WebMemberIdentity(
    Guid MemberId,
    string FullName,
    string ClubEmail,
    MemberPosition Position);

public interface IWebIdentityDirectory
{
    public Task<WebMemberIdentity?> FindActiveByIdAsync(
        Guid memberId,
        CancellationToken cancellationToken);

    public Task<WebMemberIdentity?> FindActiveByClubEmailAsync(
        string clubEmail,
        CancellationToken cancellationToken);
}

public interface IWebIdentityService
{
    public Task<WebMemberIdentity?> ResolveActiveMemberByEmailAsync(
        string? email,
        CancellationToken cancellationToken);

    public Task<WebMemberIdentity?> ResolveActiveMemberByIdAsync(
        Guid memberId,
        CancellationToken cancellationToken);
}

public static class WebIdentityPrincipal
{
    public static ClaimsPrincipal Create(
        WebMemberIdentity member,
        string authenticationType)
    {
        var identity = new ClaimsIdentity(authenticationType);
        ReplaceMemberClaims(identity, member);
        return new ClaimsPrincipal(identity);
    }

    public static void ReplaceMemberClaims(
        ClaimsIdentity identity,
        WebMemberIdentity member)
    {
        RemoveClaims(identity, ClaimTypes.NameIdentifier);
        RemoveClaims(identity, ClaimTypes.Name);
        RemoveClaims(identity, ClaimTypes.Email);
        RemoveClaims(identity, ClaimTypes.Role);
        RemoveClaims(identity, FelionClaimTypes.MemberId);
        RemoveClaims(identity, FelionClaimTypes.Position);

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, member.MemberId.ToString("D")));
        identity.AddClaim(new Claim(ClaimTypes.Name, member.FullName));
        identity.AddClaim(new Claim(ClaimTypes.Email, member.ClubEmail));
        identity.AddClaim(new Claim(ClaimTypes.Role, member.Position.ToString()));
        identity.AddClaim(new Claim(FelionClaimTypes.MemberId, member.MemberId.ToString("D")));
        identity.AddClaim(new Claim(FelionClaimTypes.Position, member.Position.ToString()));
    }

    public static bool TryGetMemberId(
        ClaimsPrincipal principal,
        out Guid memberId)
    {
        return Guid.TryParse(
            principal.FindFirst(FelionClaimTypes.MemberId)?.Value,
            out memberId)
            && memberId != Guid.Empty;
    }

    private static void RemoveClaims(ClaimsIdentity identity, string claimType)
    {
        foreach (var claim in identity.FindAll(claimType).ToArray())
        {
            identity.RemoveClaim(claim);
        }
    }
}
