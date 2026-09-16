using System.Net;
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

    public sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
