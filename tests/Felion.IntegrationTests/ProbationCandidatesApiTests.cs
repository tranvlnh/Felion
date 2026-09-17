using System.Net;
using System.Net.Http.Json;
using Felion.Application.Identity;
using Felion.Application.Probation;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Felion.IntegrationTests;

public sealed class ProbationCandidatesApiTests
{
    [Fact]
    public async Task CandidateListRequiresAnAuthenticatedCoreOrAdmin()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        using var anonymousResponse = await client.GetAsync("/api/v1/probation/candidates");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var memberRequest = CreateRequest(HttpMethod.Get, "/api/v1/probation/candidates", factory.Member.MemberId);
        using var memberResponse = await client.SendAsync(memberRequest);
        Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);
    }

    [Fact]
    public async Task CoreCanCreateCandidateThroughApiWithCorrelationId()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString("N");
        using var request = CreateRequest(
            HttpMethod.Post,
            "/api/v1/probation/candidates",
            factory.Core.MemberId,
            new
            {
                StudentId = "SV001",
                FullName = "Candidate One",
                DepartmentId = Guid.NewGuid(),
                GenerationId = Guid.NewGuid()
            });
        request.Headers.Add("X-Correlation-Id", correlationId);

        using var response = await client.SendAsync(request);
        var candidate = await response.Content.ReadFromJsonAsync<ProbationCandidateDto>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(candidate);
        Assert.Equal("SV001", candidate.StudentId);
        var created = Assert.Single(factory.Candidates.Created);
        Assert.Equal(factory.Core.MemberId, created.ActorMemberId);
        Assert.Equal(correlationId, created.CorrelationId);
    }

    [Fact]
    public async Task CandidateListRejectsUnknownStatusWithValidationProblem()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/probation/candidates?status=Unknown", factory.Core.MemberId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Status must be", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, Guid actorMemberId, object? content = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(WebIdentityHeaders.TemporaryActorMemberId, actorMemberId.ToString("D"));
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return request;
    }

    public sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        public WebMemberIdentity Core { get; } = new(Guid.NewGuid(), "Core User", "core@example.org", MemberPosition.Core);
        public WebMemberIdentity Member { get; } = new(Guid.NewGuid(), "Member User", "member@example.org", MemberPosition.Member);
        public TestCandidateService Candidates { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = string.Empty }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWebIdentityService>();
                services.AddSingleton<IWebIdentityService>(new TestWebIdentityService([Core, Member]));
                services.RemoveAll<IProbationCandidateManagementService>();
                services.AddSingleton<IProbationCandidateManagementService>(Candidates);
            });
        }
    }

    public sealed class TestWebIdentityService(IReadOnlyCollection<WebMemberIdentity> members) : IWebIdentityService
    {
        public Task<WebMemberIdentity?> ResolveActiveMemberByEmailAsync(string? email, CancellationToken cancellationToken) => Task.FromResult(members.SingleOrDefault(member => member.ClubEmail == email));
        public Task<WebMemberIdentity?> ResolveActiveMemberByIdAsync(Guid memberId, CancellationToken cancellationToken) => Task.FromResult(members.SingleOrDefault(member => member.MemberId == memberId));
    }

    public sealed class TestCandidateService : IProbationCandidateManagementService
    {
        public List<CreateCall> Created { get; } = [];

        public Task<ProbationCandidatePage> ListAsync(Guid actorMemberId, ListProbationCandidatesQuery query, CancellationToken cancellationToken) => Task.FromResult(new ProbationCandidatePage([], query.Page, query.PageSize, 0));
        public Task<ProbationCandidateDto> GetAsync(Guid actorMemberId, Guid candidateId, CancellationToken cancellationToken) => Throw<ProbationCandidateDto>();

        public Task<ProbationCandidateDto> CreateAsync(Guid actorMemberId, CreateProbationCandidateCommand command, string correlationId, CancellationToken cancellationToken)
        {
            Created.Add(new CreateCall(actorMemberId, command, correlationId));
            return Task.FromResult(CreateDto(Guid.NewGuid(), command.StudentId, command.FullName));
        }

        public Task<ProbationCandidateDto> UpdateAsync(Guid actorMemberId, Guid candidateId, UpdateProbationCandidateCommand command, string correlationId, CancellationToken cancellationToken) => Throw<ProbationCandidateDto>();
        public Task<ProbationCandidateDto> ChangeTeamAsync(Guid actorMemberId, Guid candidateId, Guid? teamId, string correlationId, CancellationToken cancellationToken) => Throw<ProbationCandidateDto>();
        public Task<ProbationManagementReferenceData> GetReferenceDataAsync(Guid actorMemberId, CancellationToken cancellationToken) => Task.FromResult(new ProbationManagementReferenceData([], [], []));
        public Task<IReadOnlyList<ProbationMentorOption>> SearchMentorsAsync(Guid actorMemberId, string? search, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProbationMentorOption>>([]);

        private static ProbationCandidateDto CreateDto(Guid id, string studentId, string fullName)
        {
            var timestamp = DateTimeOffset.UtcNow;
            return new ProbationCandidateDto(
                id,
                studentId,
                fullName,
                new ProbationCandidateReference(Guid.NewGuid(), "Technical", "technical"),
                new ProbationCandidateReference(Guid.NewGuid(), "Generation 1", "G1"),
                null,
                ProbationCandidateStatus.Active,
                false,
                timestamp,
                timestamp);
        }

        private static Task<T> Throw<T>() => Task.FromException<T>(new NotSupportedException());
    }

    public sealed record CreateCall(Guid ActorMemberId, CreateProbationCandidateCommand Command, string CorrelationId);
}
