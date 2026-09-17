using Felion.Domain.Common;

namespace Felion.Domain.Events;

public sealed class EventRegistration
{
    private EventRegistration()
    {
    }

    private EventRegistration(
        Guid id,
        Guid eventPositionId,
        Guid memberId,
        EventRegistrationStatus status,
        DateTimeOffset? requestedAt,
        DateTimeOffset? assignedAt,
        Guid? assignedByMemberId,
        DateTimeOffset now)
    {
        Id = id;
        EventPositionId = eventPositionId;
        MemberId = memberId;
        Status = status;
        RequestedAt = requestedAt;
        AssignedAt = assignedAt;
        AssignedByMemberId = assignedByMemberId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid EventPositionId { get; private set; }

    public Guid MemberId { get; private set; }

    public EventRegistrationStatus Status { get; private set; }

    public DateTimeOffset? RequestedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public Guid? DecidedByMemberId { get; private set; }

    public DateTimeOffset? AssignedAt { get; private set; }

    public Guid? AssignedByMemberId { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool ConsumesCapacity => Status is EventRegistrationStatus.Pending
        or EventRegistrationStatus.Approved
        or EventRegistrationStatus.Assigned;

    public static EventRegistration Request(
        Guid eventPositionId,
        Guid memberId,
        DateTimeOffset? now = null)
    {
        ValidateIds(eventPositionId, memberId);
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EventRegistration(
            Guid.NewGuid(),
            eventPositionId,
            memberId,
            EventRegistrationStatus.Pending,
            timestamp,
            null,
            null,
            timestamp);
    }

    public static EventRegistration Assign(
        Guid eventPositionId,
        Guid memberId,
        Guid assignedByMemberId,
        DateTimeOffset? now = null)
    {
        ValidateIds(eventPositionId, memberId);
        if (assignedByMemberId == Guid.Empty)
        {
            throw new DomainException("Assigning member is required.");
        }

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EventRegistration(
            Guid.NewGuid(),
            eventPositionId,
            memberId,
            EventRegistrationStatus.Assigned,
            null,
            timestamp,
            assignedByMemberId,
            timestamp);
    }

    public void Approve(Guid decidedByMemberId, DateTimeOffset? now = null)
    {
        Decide(EventRegistrationStatus.Approved, decidedByMemberId, now);
    }

    public void Reject(Guid decidedByMemberId, DateTimeOffset? now = null)
    {
        Decide(EventRegistrationStatus.Rejected, decidedByMemberId, now);
    }

    private void Decide(EventRegistrationStatus nextStatus, Guid decidedByMemberId, DateTimeOffset? now)
    {
        if (Status != EventRegistrationStatus.Pending)
        {
            throw new DomainException("Only a pending registration can be decided.");
        }

        if (decidedByMemberId == Guid.Empty)
        {
            throw new DomainException("Deciding member is required.");
        }

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        Status = nextStatus;
        DecidedAt = timestamp;
        DecidedByMemberId = decidedByMemberId;
        UpdatedAt = timestamp;
    }

    private static void ValidateIds(Guid eventPositionId, Guid memberId)
    {
        if (eventPositionId == Guid.Empty)
        {
            throw new DomainException("Event position is required.");
        }

        if (memberId == Guid.Empty)
        {
            throw new DomainException("Member is required.");
        }
    }
}
