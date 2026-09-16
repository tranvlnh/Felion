using Felion.Domain.Common;

namespace Felion.Domain.Probation;

public sealed class ProbationTeam
{
    private ProbationTeam()
    {
    }

    private ProbationTeam(
        Guid id,
        string name,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ProbationTeam Create(string name, DateTimeOffset? now = null)
    {
        var normalizedName = NormalizeName(name);
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new ProbationTeam(Guid.NewGuid(), normalizedName, timestamp);
    }

    public void Rename(string name, DateTimeOffset? now = null)
    {
        Name = NormalizeName(name);
        Touch(now);
    }

    public void SetActive(bool isActive, DateTimeOffset? now = null)
    {
        IsActive = isActive;
        Touch(now);
    }

    private static string NormalizeName(string name)
    {
        var normalized = IdentityNormalizer.RequiredText(name, nameof(Name));
        if (normalized.Length > 200)
        {
            throw new DomainException("Team name must be at most 200 characters.");
        }

        return normalized;
    }

    private void Touch(DateTimeOffset? now)
    {
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
