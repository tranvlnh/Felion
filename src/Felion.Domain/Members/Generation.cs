using Felion.Domain.Common;

namespace Felion.Domain.Members;

public sealed class Generation
{
    private Generation()
    {
    }

    private Generation(Guid id, string name, string code)
    {
        Id = id;
        Name = name;
        Code = code;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public static Generation Create(string name, string code)
    {
        var normalizedName = IdentityNormalizer.RequiredText(name, nameof(Name));
        var normalizedCode = IdentityNormalizer.RequiredText(code, nameof(Code)).ToUpperInvariant();

        return new Generation(Guid.NewGuid(), normalizedName, normalizedCode);
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
