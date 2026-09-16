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
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Status = EvaluationPeriodStatus.Draft;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset? StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public EvaluationPeriodStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static EvaluationPeriod Create(
        string name,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        DateTimeOffset? now = null)
    {
        var normalizedName = NormalizeName(name);
        var normalizedStartsAt = NormalizeTimestamp(startsAt);
        var normalizedEndsAt = NormalizeTimestamp(endsAt);
        ValidateSchedule(normalizedStartsAt, normalizedEndsAt);

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EvaluationPeriod(
            Guid.NewGuid(),
            normalizedName,
            normalizedStartsAt,
            normalizedEndsAt,
            timestamp);
    }

    public void Open(DateTimeOffset? now = null)
    {
        if (Status != EvaluationPeriodStatus.Draft)
        {
            throw new DomainException("Only a draft evaluation period can be opened.");
        }

        Status = EvaluationPeriodStatus.Open;
        Touch(now);
    }

    public void Close(DateTimeOffset? now = null)
    {
        if (Status != EvaluationPeriodStatus.Open)
        {
            throw new DomainException("Only an open evaluation period can be closed.");
        }

        Status = EvaluationPeriodStatus.Closed;
        Touch(now);
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

    private static DateTimeOffset? NormalizeTimestamp(DateTimeOffset? value)
    {
        return value?.ToUniversalTime();
    }

    private static void ValidateSchedule(DateTimeOffset? startsAt, DateTimeOffset? endsAt)
    {
        if (startsAt is not null && endsAt is not null && endsAt <= startsAt)
        {
            throw new DomainException("Evaluation period end must be later than its start.");
        }
    }

    private void Touch(DateTimeOffset? now)
    {
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
