using Felion.Application.Hardening;
using Felion.Infrastructure.RateLimiting;

namespace Felion.IntegrationTests;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task DiscordLinkLimitAllowsFiveAttemptsPerDiscordUserInTenMinutes()
    {
        using var gate = new InMemoryRateLimitGate();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var decision = await gate.TryAcquireAsync(
                RateLimitOperation.DiscordLink,
                "123456789",
                CancellationToken.None);
            Assert.True(decision.IsAcquired);
        }

        var rejected = await gate.TryAcquireAsync(
            RateLimitOperation.DiscordLink,
            "123456789",
            CancellationToken.None);
        var independentUser = await gate.TryAcquireAsync(
            RateLimitOperation.DiscordLink,
            "987654321",
            CancellationToken.None);

        Assert.False(rejected.IsAcquired);
        Assert.True(independentUser.IsAcquired);
    }

    [Fact]
    public async Task EvaluationSubmissionLimitAllowsTenAttemptsPerReviewerInOneMinute()
    {
        using var gate = new InMemoryRateLimitGate();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var decision = await gate.TryAcquireAsync(
                RateLimitOperation.EvaluationSubmission,
                "Mentor:reviewer-1",
                CancellationToken.None);
            Assert.True(decision.IsAcquired);
        }

        var rejected = await gate.TryAcquireAsync(
            RateLimitOperation.EvaluationSubmission,
            "Mentor:reviewer-1",
            CancellationToken.None);
        var independentReviewer = await gate.TryAcquireAsync(
            RateLimitOperation.EvaluationSubmission,
            "Mentor:reviewer-2",
            CancellationToken.None);

        Assert.False(rejected.IsAcquired);
        Assert.True(independentReviewer.IsAcquired);
    }
}
