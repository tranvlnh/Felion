using Felion.Domain.Common;

namespace Felion.Domain.Events;

public sealed class EventAttendance
{
    private EventAttendance()
    {
    }

    private EventAttendance(
        Guid id,
        Guid eventId,
        Guid memberId,
        DateTimeOffset checkedInAt,
        Guid checkedInByMemberId,
        DateTimeOffset createdAt)
    {
        Id = id;
        EventId = eventId;
        MemberId = memberId;
        CheckedInAt = checkedInAt;
        CheckedInByMemberId = checkedInByMemberId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public Guid MemberId { get; private set; }

    public DateTimeOffset CheckedInAt { get; private set; }

    public Guid CheckedInByMemberId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static EventAttendance CheckIn(
        Guid eventId,
        Guid memberId,
        Guid checkedInByMemberId,
        DateTimeOffset? now = null)
    {
        if (eventId == Guid.Empty)
        {
            throw new DomainException("Event is required.");
        }

        if (memberId == Guid.Empty)
        {
            throw new DomainException("Member is required.");
        }

        if (checkedInByMemberId == Guid.Empty)
        {
            throw new DomainException("Checking-in member is required.");
        }

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EventAttendance(Guid.NewGuid(), eventId, memberId, timestamp, checkedInByMemberId, timestamp);
    }
}
