using Felion.Domain.Common;

namespace Felion.Domain.Evaluation;

public sealed class EvaluationPeriod
{
    private EvaluationPeriod()
    {
    }

    private EvaluationPeriod(
        Guid id,
        string name,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Status = EvaluationPeriodStatus.Open;
        CreatedAt = now;
        UpdatedAt = now;
        OpenedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public EvaluationPeriodStatus Status { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static EvaluationPeriod Create(
        string name,
        DateTimeOffset? now = null)
    {
        var normalizedName = NormalizeName(name);
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EvaluationPeriod(Guid.NewGuid(), normalizedName, timestamp);
    }

    public void Close(DateTimeOffset? now = null)
    {
        if (Status != EvaluationPeriodStatus.Open)
        {
            throw new DomainException("Only an open evaluation period can be closed.");
        }

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        Status = EvaluationPeriodStatus.Closed;
        ClosedAt = timestamp;
        Touch(timestamp);
    }

    private static string NormalizeName(string name)
    {
        var normalized = IdentityNormalizer.RequiredText(name, nameof(Name));
        if (normalized.Length > 200)
        {
            throw new DomainException("Evaluation period name must be at most 200 characters.");
        }

        return normalized;
    }

    private void Touch(DateTimeOffset? now)
    {
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
