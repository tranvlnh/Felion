using Felion.Domain.Common;
using Felion.Domain.Members;

namespace Felion.Domain.Identity;

public sealed class DiscordRoleMapping
{
    private DiscordRoleMapping()
    {
    }

    private DiscordRoleMapping(
        Guid id,
        DiscordRoleMappingKind kind,
        string subjectKey,
        long discordRoleId,
        string roleNameSnapshot,
        DateTimeOffset now)
    {
        Id = id;
        Kind = kind;
        SubjectKey = subjectKey;
        DiscordRoleId = discordRoleId;
        RoleNameSnapshot = roleNameSnapshot;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public DiscordRoleMappingKind Kind { get; private set; }

    public string SubjectKey { get; private set; } = string.Empty;

    public long DiscordRoleId { get; private set; }

    public string RoleNameSnapshot { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DiscordRoleMapping Create(
        DiscordRoleMappingKind kind,
        string subjectKey,
        long discordRoleId,
        string roleNameSnapshot,
        DateTimeOffset? now = null)
    {
        var normalizedKey = NormalizeSubjectKey(kind, subjectKey);
        ValidateRole(discordRoleId, roleNameSnapshot);
        return new DiscordRoleMapping(
            Guid.NewGuid(),
            kind,
            normalizedKey,
            discordRoleId,
            IdentityNormalizer.RequiredText(roleNameSnapshot, nameof(RoleNameSnapshot)),
            (now ?? DateTimeOffset.UtcNow).ToUniversalTime());
    }

    public void UpdateRole(long discordRoleId, string roleNameSnapshot, DateTimeOffset? now = null)
    {
        ValidateRole(discordRoleId, roleNameSnapshot);
        DiscordRoleId = discordRoleId;
        RoleNameSnapshot = IdentityNormalizer.RequiredText(roleNameSnapshot, nameof(RoleNameSnapshot));
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    public static string NormalizeSubjectKey(DiscordRoleMappingKind kind, string subjectKey)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("Unknown Discord role mapping kind.");
        }

        var value = IdentityNormalizer.RequiredText(subjectKey, nameof(SubjectKey));
        return kind switch
        {
            DiscordRoleMappingKind.Position => NormalizePosition(value),
            DiscordRoleMappingKind.Probation => NormalizeProbation(value),
            DiscordRoleMappingKind.Department
                or DiscordRoleMappingKind.Generation
                or DiscordRoleMappingKind.ProbationTeam => NormalizeGuid(value),
            _ => throw new DomainException("Unknown Discord role mapping kind.")
        };
    }

    private static string NormalizePosition(string value)
    {
        if (!Enum.TryParse<MemberPosition>(value, ignoreCase: true, out var position)
            || !Enum.IsDefined(position))
        {
            throw new DomainException("Position role mapping key must be Admin, Core or Member.");
        }

        return position.ToString();
    }

    private static string NormalizeProbation(string value)
    {
        if (!string.Equals(value, nameof(DiscordIdentitySubjectType.Probation), StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Probation role mapping key must be Probation.");
        }

        return nameof(DiscordIdentitySubjectType.Probation);
    }

    private static string NormalizeGuid(string value)
    {
        if (!Guid.TryParse(value, out var subjectId) || subjectId == Guid.Empty)
        {
            throw new DomainException("Role mapping subject key must be a non-empty GUID.");
        }

        return subjectId.ToString("D");
    }

    private static void ValidateRole(long discordRoleId, string roleNameSnapshot)
    {
        if (discordRoleId <= 0)
        {
            throw new DomainException("Discord role ID must be a positive signed snowflake.");
        }

        if (IdentityNormalizer.RequiredText(roleNameSnapshot, nameof(RoleNameSnapshot)).Length > 100)
        {
            throw new DomainException("Discord role name must be at most 100 characters.");
        }
    }
}
