using System.Security.Claims;
using Felion.Application.Identity;
using Felion.Domain.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Felion.IntegrationTests;

public sealed class WebAuthorizationTests : IClassFixture<WebAuthorizationTests.TestApplicationFactory>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public WebAuthorizationTests(TestApplicationFactory factory)
    {
        _scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task AuthorizationPoliciesEnforceAdminCoreMemberMatrix()
    {
        using var scope = _scopeFactory.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var admin = WebIdentityPrincipal.Create(CreateMember(MemberPosition.Admin), "Test");
        var core = WebIdentityPrincipal.Create(CreateMember(MemberPosition.Core), "Test");
        var member = WebIdentityPrincipal.Create(CreateMember(MemberPosition.Member), "Test");

        Assert.True((await authorization.AuthorizeAsync(admin, null, FelionAuthorizationPolicies.ActiveMember)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(admin, null, FelionAuthorizationPolicies.CoreOrAdmin)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(admin, null, FelionAuthorizationPolicies.AdminOnly)).Succeeded);

        Assert.True((await authorization.AuthorizeAsync(core, null, FelionAuthorizationPolicies.ActiveMember)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(core, null, FelionAuthorizationPolicies.CoreOrAdmin)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(core, null, FelionAuthorizationPolicies.AdminOnly)).Succeeded);

        Assert.True((await authorization.AuthorizeAsync(member, null, FelionAuthorizationPolicies.ActiveMember)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(member, null, FelionAuthorizationPolicies.CoreOrAdmin)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(member, null, FelionAuthorizationPolicies.AdminOnly)).Succeeded);
    }

    [Fact]
    public async Task AuthorizationPoliciesRejectProbationOnlyPrincipal()
    {
        using var scope = _scopeFactory.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var probationPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D"))],
            "DiscordOnly"));

        var result = await authorization.AuthorizeAsync(
            probationPrincipal,
            null,
            FelionAuthorizationPolicies.ActiveMember);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LoginWithoutGoogleCredentialsReturnsServiceUnavailable()
    {
        using var client = new TestApplicationFactory().CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/api/v1/auth/login");

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static WebMemberIdentity CreateMember(MemberPosition position)
    {
        return new WebMemberIdentity(
            Guid.NewGuid(),
            $"{position} Member",
            $"{position.ToString().ToLowerInvariant()}@example.org",
            position);
    }

    public sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
