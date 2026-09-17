using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Felion.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<HealthEndpointTests.TestApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task LiveHealthEndpointReturnsOk()
    {
        using var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResponsesIncludeApiSecurityHeaders()
    {
        using var response = await _client.GetAsync("/health/live");

        Assert.Equal("default-src 'none'; base-uri 'none'; frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("camera=(), geolocation=(), microphone=()", response.Headers.GetValues("Permissions-Policy").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task MissingEndpointReturnsProblemDetailsWithCorrelationId()
    {
        var suppliedCorrelationId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/missing");
        request.Headers.Add("X-Correlation-Id", suppliedCorrelationId.ToString());

        using var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(suppliedCorrelationId.ToString("N"), response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Contains(suppliedCorrelationId.ToString("N"), responseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MemberManagementRequiresTemporaryActorHeader()
    {
        using var response = await _client.GetAsync("/api/v1/members");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProbationDashboardStaticShellIsServedWithLocalAssetCsp()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/probation/index.html");
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Probation Admin", body, StringComparison.Ordinal);
        Assert.Contains("Members &amp; Discord roles", body, StringComparison.Ordinal);
        Assert.Contains("Thêm member", body, StringComparison.Ordinal);
        Assert.Contains("script-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Contains("no-store", response.Headers.GetValues("Cache-Control").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscordRoleCreationRequiresTemporaryActorHeader()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/discord/roles",
            new { Name = "Felion" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
