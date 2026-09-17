using System.Threading.RateLimiting;
using Felion.Application.Hardening;

namespace Felion.Infrastructure.RateLimiting;

public sealed class InMemoryRateLimitGate : IRateLimitGate, IDisposable
{
    private const int DiscordLinkPermitLimit = 5;
    private const int EvaluationSubmissionPermitLimit = 10;

    private static readonly TimeSpan DiscordLinkWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EvaluationSubmissionWindow = TimeSpan.FromMinutes(1);

    private readonly PartitionedRateLimiter<string> _discordLinkLimiter = CreateLimiter(
        DiscordLinkPermitLimit,
        DiscordLinkWindow);
    private readonly PartitionedRateLimiter<string> _evaluationSubmissionLimiter = CreateLimiter(
        EvaluationSubmissionPermitLimit,
        EvaluationSubmissionWindow);

    public ValueTask<RateLimitDecision> TryAcquireAsync(
        RateLimitOperation operation,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        cancellationToken.ThrowIfCancellationRequested();

        var limiter = operation switch
        {
            RateLimitOperation.DiscordLink => _discordLinkLimiter,
            RateLimitOperation.EvaluationSubmission => _evaluationSubmissionLimiter,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown rate limit operation.")
        };

        using var lease = limiter.AttemptAcquire(partitionKey);
        TimeSpan? retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan metadata)
            ? metadata
            : null;
        return ValueTask.FromResult(new RateLimitDecision(lease.IsAcquired, retryAfter));
    }

    public void Dispose()
    {
        _discordLinkLimiter.Dispose();
        _evaluationSubmissionLimiter.Dispose();
    }

    private static PartitionedRateLimiter<string> CreateLimiter(
        int permitLimit,
        TimeSpan window)
    {
        return PartitionedRateLimiter.Create<string, string>(partitionKey =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = permitLimit,
                    QueueLimit = 0,
                    Window = window
                }));
    }
}
