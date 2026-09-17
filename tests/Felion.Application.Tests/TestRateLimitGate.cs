using Felion.Application.Hardening;

namespace Felion.Application.Tests;

internal sealed class TestRateLimitGate(
    bool isAcquired = true,
    TimeSpan? retryAfter = null) : IRateLimitGate
{
    public List<(RateLimitOperation Operation, string PartitionKey)> Attempts { get; } = [];

    public ValueTask<RateLimitDecision> TryAcquireAsync(
        RateLimitOperation operation,
        string partitionKey,
        CancellationToken cancellationToken)
    {
        Attempts.Add((operation, partitionKey));
        return ValueTask.FromResult(new RateLimitDecision(isAcquired, retryAfter));
    }
}
