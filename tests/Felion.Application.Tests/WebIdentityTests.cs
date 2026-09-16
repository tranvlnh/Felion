using System.Security.Claims;
using Felion.Application.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class WebIdentityTests
{
    [Fact]
    public async Task ResolveActiveMemberByEmailNormalizesEmailBeforeLookup()
    {
        var member = new WebMemberIdentity(
            Guid.NewGuid(),
            "Student One",
            "student@example.org",
            MemberPosition.Member);
        var directory = new FakeWebIdentityDirectory { EmailResult = member };
        var service = new WebIdentityService(directory);

        var result = await service.ResolveActiveMemberByEmailAsync(
            "  STUDENT@EXAMPLE.ORG ",
            CancellationToken.None);

        Assert.Equal(member, result);
        Assert.Equal("student@example.org", directory.LastEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveActiveMemberByEmailMissingEmailReturnsNull(string? email)
    {
        var directory = new FakeWebIdentityDirectory();
        var service = new WebIdentityService(directory);

        var result = await service.ResolveActiveMemberByEmailAsync(email, CancellationToken.None);

        Assert.Null(result);
        Assert.Null(directory.LastEmail);
    }

    [Fact]
    public async Task ResolveActiveMemberByEmailUnknownOrInactiveMemberReturnsNull()
    {
        var service = new WebIdentityService(new FakeWebIdentityDirectory());

        var result = await service.ResolveActiveMemberByEmailAsync(
            "unknown@example.org",
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void CreatePrincipalContainsOnlyApplicationAuthorizationIdentity()
    {
        var member = new WebMemberIdentity(
            Guid.NewGuid(),
            "Student One",
            "student@example.org",
            MemberPosition.Core);

        var principal = WebIdentityPrincipal.Create(member, "Test");

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal(member.MemberId.ToString("D"), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(member.MemberId.ToString("D"), principal.FindFirst(FelionClaimTypes.MemberId)?.Value);
        Assert.Equal(MemberPosition.Core.ToString(), principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.Equal(MemberPosition.Core.ToString(), principal.FindFirst(FelionClaimTypes.Position)?.Value);
        Assert.True(WebIdentityPrincipal.TryGetMemberId(principal, out var memberId));
        Assert.Equal(member.MemberId, memberId);
    }

    private sealed class FakeWebIdentityDirectory : IWebIdentityDirectory
    {
        public WebMemberIdentity? EmailResult { get; init; }

        public string? LastEmail { get; private set; }

        public Task<WebMemberIdentity?> FindActiveByIdAsync(
            Guid memberId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<WebMemberIdentity?>(null);
        }

        public Task<WebMemberIdentity?> FindActiveByClubEmailAsync(
            string clubEmail,
            CancellationToken cancellationToken)
        {
            LastEmail = clubEmail;
            return Task.FromResult(EmailResult);
        }
    }
}
