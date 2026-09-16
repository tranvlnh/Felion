namespace Felion.Domain.Common;

public static class IdentityNormalizer
{
    public static string StudentId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            throw new DomainException("StudentId is required.");
        }

        return normalized;
    }

    public static string ClubEmail(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            throw new DomainException("Club email is required.");
        }

        return normalized;
    }

    public static string RequiredText(string value, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(value);

        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new DomainException($"{fieldName} is required.");
        }

        return normalized;
    }
}
