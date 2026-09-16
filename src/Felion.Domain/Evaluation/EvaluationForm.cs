using Felion.Domain.Common;

namespace Felion.Domain.Evaluation;

public sealed class EvaluationForm
{
    private EvaluationForm()
    {
    }

    private EvaluationForm(
        Guid id,
        Guid periodId,
        string name,
        EvaluationReviewerType reviewerType,
        DateTimeOffset now)
    {
        Id = id;
        PeriodId = periodId;
        Name = name;
        ReviewerType = reviewerType;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid PeriodId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public EvaluationReviewerType ReviewerType { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static EvaluationForm Create(
        Guid periodId,
        string name,
        EvaluationReviewerType reviewerType,
        DateTimeOffset? now = null)
    {
        if (periodId == Guid.Empty)
        {
            throw new DomainException("Evaluation period is required.");
        }

        var normalizedName = IdentityNormalizer.RequiredText(name, nameof(Name));
        if (normalizedName.Length > 200)
        {
            throw new DomainException("Evaluation form name must be at most 200 characters.");
        }

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EvaluationForm(Guid.NewGuid(), periodId, normalizedName, reviewerType, timestamp);
    }

    public void Update(string? name, bool? isActive, DateTimeOffset? now = null)
    {
        if (name is null && isActive is null)
        {
            Touch(now);
            return;
        }

        if (name is not null)
        {
            var normalizedName = IdentityNormalizer.RequiredText(name, nameof(Name));
            if (normalizedName.Length > 200)
            {
                throw new DomainException("Evaluation form name must be at most 200 characters.");
            }

            Name = normalizedName;
        }

        if (isActive is not null)
        {
            IsActive = isActive.Value;
        }

        Touch(now);
    }

    private void Touch(DateTimeOffset? now)
    {
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
