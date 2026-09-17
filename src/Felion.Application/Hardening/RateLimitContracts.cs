namespace Felion.Application.Hardening;

public enum RateLimitOperation
{
    DiscordLink,
    EvaluationSubmission
}

public readonly record struct RateLimitDecision(bool IsAcquired, TimeSpan? RetryAfter);

public interface IRateLimitGate
{
    public ValueTask<RateLimitDecision> TryAcquireAsync(
        RateLimitOperation operation,
        string partitionKey,
        CancellationToken cancellationToken);
}

public sealed class RateLimitExceededException(
    RateLimitOperation operation,
    TimeSpan? retryAfter)
    : Exception("Too many requests. Please try again later.")
{
    public RateLimitOperation Operation { get; } = operation;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
