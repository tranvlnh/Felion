using Felion.Domain.Common;

namespace Felion.Domain.Identity;

public sealed class DiscordRoleAssignment
{
    private DiscordRoleAssignment()
    {
    }

    private DiscordRoleAssignment(
        Guid id,
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        long discordRoleId,
        string roleNameSnapshot,
        DateTimeOffset now)
    {
        Id = id;
        SubjectType = subjectType;
        SubjectId = subjectId;
        DiscordRoleId = discordRoleId;
        RoleNameSnapshot = roleNameSnapshot;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public DiscordIdentitySubjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public long DiscordRoleId { get; private set; }

    public string RoleNameSnapshot { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DiscordRoleAssignment Create(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        long discordRoleId,
        string roleNameSnapshot,
        DateTimeOffset? now = null)
    {
        if (!Enum.IsDefined(subjectType))
        {
            throw new DomainException("Unknown Discord identity subject type.");
        }

        if (subjectId == Guid.Empty)
        {
            throw new DomainException("Discord role assignment subject is required.");
        }

        ValidateRole(discordRoleId, roleNameSnapshot);
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new DiscordRoleAssignment(
            Guid.NewGuid(),
            subjectType,
            subjectId,
            discordRoleId,
            IdentityNormalizer.RequiredText(roleNameSnapshot, nameof(RoleNameSnapshot)),
            timestamp);
    }

    public void TransferToMember(Guid memberId, DateTimeOffset? now = null)
    {
        if (SubjectType != DiscordIdentitySubjectType.Probation)
        {
            throw new DomainException("Only probation role assignments can be transferred to a Member.");
        }

        if (memberId == Guid.Empty)
        {
            throw new DomainException("Member is required for a role assignment transfer.");
        }

        SubjectType = DiscordIdentitySubjectType.Member;
        SubjectId = memberId;
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
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
