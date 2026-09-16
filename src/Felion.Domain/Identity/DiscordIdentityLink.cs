using Felion.Domain.Common;

namespace Felion.Domain.Identity;

public sealed class DiscordIdentityLink
{
    private DiscordIdentityLink()
    {
    }

    private DiscordIdentityLink(
        Guid id,
        long discordUserId,
        string studentId,
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        DateTimeOffset linkedAt)
    {
        Id = id;
        DiscordUserId = discordUserId;
        StudentId = studentId;
        SubjectType = subjectType;
        SubjectId = subjectId;
        LinkedAt = linkedAt;
    }

    public Guid Id { get; private set; }

    public long DiscordUserId { get; private set; }

    public string StudentId { get; private set; } = string.Empty;

    public DiscordIdentitySubjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }

    public static DiscordIdentityLink Create(
        long discordUserId,
        string studentId,
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        DateTimeOffset? linkedAt = null)
    {
        if (discordUserId <= 0)
        {
            throw new DomainException("Discord user ID must be a positive signed snowflake.");
        }

        if (subjectId == Guid.Empty)
        {
            throw new DomainException("Identity subject is required.");
        }

        if (!Enum.IsDefined(subjectType))
        {
            throw new DomainException("Unknown Discord identity subject type.");
        }

        return new DiscordIdentityLink(
            Guid.NewGuid(),
            discordUserId,
            IdentityNormalizer.StudentId(studentId),
            subjectType,
            subjectId,
            (linkedAt ?? DateTimeOffset.UtcNow).ToUniversalTime());
    }

    public void Relink(long discordUserId, DateTimeOffset? linkedAt = null)
    {
        if (discordUserId <= 0)
        {
            throw new DomainException("Discord user ID must be a positive signed snowflake.");
        }

        DiscordUserId = discordUserId;
        LinkedAt = (linkedAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
