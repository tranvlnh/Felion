using Felion.Domain.Common;

namespace Felion.Domain.Members;

public sealed class Department
{
    public static readonly Guid CoreDepartmentId = new("4f8d4a6b-6a1d-4b5c-9ef7-8b9b8d1f4b21");

    private Department()
    {
    }

    private Department(Guid id, string name, string slug, bool isCore)
    {
        Id = id;
        Name = name;
        Slug = slug;
        IsCore = isCore;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public bool IsCore { get; private set; }

    public bool IsActive { get; private set; }

    public static Department CreateRegular(string name, string slug)
    {
        var normalizedName = IdentityNormalizer.RequiredText(name, nameof(Name));
        var normalizedSlug = IdentityNormalizer.RequiredText(slug, nameof(Slug)).ToLowerInvariant();

        if (normalizedSlug == "core")
        {
            throw new DomainException("The Core department is reserved.");
        }

        return new Department(Guid.NewGuid(), normalizedName, normalizedSlug, isCore: false);
    }

    public static Department CreateCore()
    {
        return new Department(CoreDepartmentId, "Core", "core", isCore: true);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
