using Felion.Domain.Common;
using Felion.Domain.Events;

namespace Felion.Domain.Tests;

public sealed class EventTests
{
    [Fact]
    public void EventRequiresPositionBeforeItCanBePublishedAndFollowsLifecycle()
    {
        var @event = Event.Create(
            " Welcome Day ",
            null,
            " Hall A ",
            new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(7)),
            null,
            false,
            Guid.NewGuid());

        Assert.Equal(EventStatus.Draft, @event.Status);
        Assert.Throws<DomainException>(() => @event.Publish(hasPositions: false));

        @event.Publish(hasPositions: true);
        @event.CloseRegistration();
        @event.Start();
        @event.Complete();

        Assert.Equal(EventStatus.Completed, @event.Status);
        Assert.Throws<DomainException>(() => @event.Cancel());
    }

    [Fact]
    public void EventRejectsInvalidScheduleAndTerminalEdits()
    {
        var startsAt = DateTimeOffset.UtcNow;
        Assert.Throws<DomainException>(() => Event.Create(
            "Invalid",
            null,
            null,
            startsAt,
            startsAt,
            false,
            Guid.NewGuid()));

        var @event = Event.Create("Event", null, null, startsAt, null, false, Guid.NewGuid());
        @event.Cancel();

        Assert.Throws<DomainException>(() => @event.Update("Changed", null, null, null, null, null));
    }

    [Fact]
    public void EventPositionRequiresPositiveCapacityAndNonNegativeSortOrder()
    {
        var eventId = Guid.NewGuid();

        var position = EventPosition.Create(eventId, " Media ", null, 2, null, 0);

        Assert.Equal("Media", position.Name);
        Assert.Throws<DomainException>(() => EventPosition.Create(eventId, "Invalid", null, 0, null, 0));
        Assert.Throws<DomainException>(() => EventPosition.Create(eventId, "Invalid", null, 1, null, -1));
    }

    [Fact]
    public void RegistrationTransitionsPendingRequestAndDirectAssignmentCorrectly()
    {
        var registration = EventRegistration.Request(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(registration.ConsumesCapacity);
        Assert.Equal(EventRegistrationStatus.Pending, registration.Status);
        registration.Approve(Guid.NewGuid());
        Assert.Equal(EventRegistrationStatus.Approved, registration.Status);
        Assert.True(registration.ConsumesCapacity);
        Assert.Throws<DomainException>(() => registration.Reject(Guid.NewGuid()));

        var assignment = EventRegistration.Assign(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(EventRegistrationStatus.Assigned, assignment.Status);
        Assert.NotNull(assignment.AssignedAt);
    }

    [Fact]
    public void AttendanceRequiresEventMemberAndCheckingInMember()
    {
        var attendance = EventAttendance.CheckIn(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, attendance.Id);
        Assert.True(attendance.CheckedInAt.Offset == TimeSpan.Zero);
        Assert.Throws<DomainException>(() => EventAttendance.CheckIn(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<DomainException>(() => EventAttendance.CheckIn(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => EventAttendance.CheckIn(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));
    }
}
