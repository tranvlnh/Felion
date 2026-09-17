using System.Net;
using System.Net.Http.Json;
using Felion.Application.Hardening;
using Felion.Application.Identity;
using Felion.Application.Probation;
using Felion.Domain.Evaluation;
using Felion.Domain.Members;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Felion.IntegrationTests;

public sealed class EvaluationRateLimitApiTests
{
    [Fact]
    public async Task MentorSubmissionReturnsTooManyRequestsWithRetryAfter()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/probation/evaluations/mentor")
        {
            Content = JsonContent.Create(new
            {
                FormId = Guid.NewGuid(),
                TargetCandidateId = Guid.NewGuid(),
                Answers = Array.Empty<object>()
            })
        };
        request.Headers.Add(WebIdentityHeaders.TemporaryActorMemberId, factory.Member.MemberId.ToString("D"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        Assert.Equal("60", Assert.Single(values));
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        public WebMemberIdentity Member { get; } = new(
            Guid.NewGuid(),
            "Mentor",
            "mentor@example.org",
            MemberPosition.Member);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = string.Empty
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWebIdentityService>();
                services.AddSingleton<IWebIdentityService>(new TestWebIdentityService(Member));
                services.RemoveAll<IEvaluationManagementService>();
                services.AddSingleton<IEvaluationManagementService, RateLimitedEvaluationService>();
            });
        }
    }

    private sealed class TestWebIdentityService(WebMemberIdentity member) : IWebIdentityService
    {
        public Task<WebMemberIdentity?> ResolveActiveMemberByEmailAsync(string? email, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Equals(email, member.ClubEmail, StringComparison.OrdinalIgnoreCase)
                ? member
                : null);
        }

        public Task<WebMemberIdentity?> ResolveActiveMemberByIdAsync(Guid memberId, CancellationToken cancellationToken)
        {
            return Task.FromResult(memberId == member.MemberId ? member : null);
        }
    }

    private sealed class RateLimitedEvaluationService : IEvaluationManagementService
    {
        public Task<IReadOnlyList<EvaluationPeriodDto>> ListPeriodsAsync(Guid actorMemberId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<EvaluationPeriodDto>>([]);

        public Task<EvaluationPeriodDto> CreatePeriodAsync(Guid actorMemberId, CreateEvaluationPeriodCommand command, string correlationId, CancellationToken cancellationToken)
            => Throw<EvaluationPeriodDto>();

        public Task<EvaluationPeriodDto> OpenPeriodAsync(Guid actorMemberId, Guid periodId, string correlationId, CancellationToken cancellationToken)
            => Throw<EvaluationPeriodDto>();

        public Task<EvaluationPeriodDto> ClosePeriodAsync(Guid actorMemberId, Guid periodId, string correlationId, CancellationToken cancellationToken)
            => Throw<EvaluationPeriodDto>();

        public Task<EvaluationFormDto> CreateFormAsync(Guid actorMemberId, CreateEvaluationFormCommand command, string correlationId, CancellationToken cancellationToken)
            => Throw<EvaluationFormDto>();

        public Task<EvaluationFormDto> UpdateFormAsync(Guid actorMemberId, Guid formId, UpdateEvaluationFormCommand command, string correlationId, CancellationToken cancellationToken)
            => Throw<EvaluationFormDto>();

        public Task<EvaluationSubmissionReceiptDto> SubmitPeerEvaluationAsync(Guid reviewerCandidateId, SubmitEvaluationCommand command, CancellationToken cancellationToken)
            => Throw<EvaluationSubmissionReceiptDto>();

        public Task<EvaluationSubmissionReceiptDto> SubmitMentorEvaluationAsync(Guid reviewerMemberId, SubmitEvaluationCommand command, CancellationToken cancellationToken)
        {
            return Task.FromException<EvaluationSubmissionReceiptDto>(new RateLimitExceededException(
                RateLimitOperation.EvaluationSubmission,
                TimeSpan.FromMinutes(1)));
        }

        public Task<IReadOnlyList<EvaluationSubmissionDto>> ListResultsAsync(Guid actorMemberId, Guid? periodId, Guid? formId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<EvaluationSubmissionDto>>([]);

        private static Task<T> Throw<T>() => Task.FromException<T>(new NotSupportedException());
    }
}
