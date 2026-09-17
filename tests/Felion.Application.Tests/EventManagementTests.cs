using Felion.Application.Events;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Events;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class EventManagementTests
{
    [Fact]
    public async Task CoreCreatesEventWithAuditAndCanPublishAfterAddingPosition()
    {
        var fixture = CreateFixture();
        var service = new EventManagementService(fixture.EventStore, fixture.MemberStore);

        var @event = await service.CreateAsync(
            fixture.Core.Id,
            new CreateEventCommand(" Welcome Day ", null, "Hall A", DateTimeOffset.UtcNow.AddDays(7), null),
            "event-create",
            CancellationToken.None);

        Assert.Equal(EventStatus.Draft, @event.Status);
        Assert.Contains(fixture.EventStore.AuditLogs, audit => audit.Action == "EventCreated");
        await Assert.ThrowsAsync<EventValidationException>(() => service.PublishAsync(
            fixture.Core.Id,
            @event.Id,
            "event-publish-without-position",
            CancellationToken.None));

        var position = await service.AddPositionAsync(
            fixture.Core.Id,
            @event.Id,
            new CreateEventPositionCommand("Reception", null, 3, fixture.Department.Id, 0),
            "event-position-create",
            CancellationToken.None);
        var published = await service.PublishAsync(
            fixture.Core.Id,
            @event.Id,
            "event-publish",
            CancellationToken.None);

        Assert.Equal(EventStatus.Published, published.Status);
        Assert.Equal(position.Id, Assert.Single(published.Positions).Id);
        Assert.Contains(fixture.EventStore.AuditLogs, audit => audit.Action == "EventPositionCreated");
        Assert.Contains(fixture.EventStore.AuditLogs, audit => audit.Action == "EventPublished");
    }

    [Fact]
    public async Task MemberCannotManageEventsAndCannotSeeDraftEvents()
    {
        var fixture = CreateFixture();
        var service = new EventManagementService(fixture.EventStore, fixture.MemberStore);
        var @event = await service.CreateAsync(
            fixture.Core.Id,
            new CreateEventCommand("Internal Planning", null, null, DateTimeOffset.UtcNow.AddDays(2), null),
            "event-create",
            CancellationToken.None);

        await Assert.ThrowsAsync<EventAccessDeniedException>(() => service.UpdateAsync(
            fixture.Member.Id,
            @event.Id,
            new UpdateEventCommand(Name: "Changed"),
            "event-update",
            CancellationToken.None));
        await Assert.ThrowsAsync<EventNotFoundException>(() => service.GetAsync(
            fixture.Member.Id,
            @event.Id,
            CancellationToken.None));
        Assert.Empty(await service.ListAsync(fixture.Member.Id, CancellationToken.None));
    }

    [Fact]
    public async Task PositionRejectsInactiveRequiredDepartment()
    {
        var fixture = CreateFixture();
        var inactiveDepartment = Department.CreateRegular("Inactive", "inactive");
        inactiveDepartment.SetActive(false);
        fixture.MemberStore.Departments.Add(inactiveDepartment);
        var service = new EventManagementService(fixture.EventStore, fixture.MemberStore);
        var @event = await service.CreateAsync(
            fixture.Core.Id,
            new CreateEventCommand("Event", null, null, DateTimeOffset.UtcNow.AddDays(1), null),
            "event-create",
            CancellationToken.None);

        await Assert.ThrowsAsync<EventValidationException>(() => service.AddPositionAsync(
            fixture.Core.Id,
            @event.Id,
            new CreateEventPositionCommand("Position", null, 1, inactiveDepartment.Id, 0),
            "event-position",
            CancellationToken.None));
    }

    [Fact]
    public async Task MemberRegistrationUsesPendingCapacityAndCanBeApprovedWithAudit()
    {
        var fixture = CreateFixture();
        var service = new EventManagementService(fixture.EventStore, fixture.MemberStore);
        var (eventId, positionId) = await CreatePublishedEventAsync(service, fixture);

        var registration = await service.RegisterAsync(
            fixture.Member.Id,
            eventId,
            positionId,
            "registration-request",
            CancellationToken.None);

        Assert.Equal(EventRegistrationStatus.Pending, registration.Status);
        Assert.Equal(1, await fixture.EventStore.CountCapacityConsumingRegistrationsAsync(positionId, CancellationToken.None));
        var approved = await service.ApproveRegistrationAsync(
            fixture.Core.Id,
            eventId,
            registration.Id,
            "registration-approve",
            CancellationToken.None);

        Assert.Equal(EventRegistrationStatus.Approved, approved.Status);
        Assert.Equal(fixture.Core.Id, approved.DecidedByMemberId);
        Assert.Contains(fixture.EventStore.AuditLogs, audit => audit.Action == "EventRegistrationRequested");
        Assert.Contains(fixture.EventStore.AuditLogs, audit => audit.Action == "EventRegistrationApproved");
    }

    [Fact]
    public async Task RejectionReleasesPendingCapacityForAnotherMember()
    {
        var fixture = CreateFixture();
        var service = new EventManagementService(fixture.EventStore, fixture.MemberStore);
        var (eventId, positionId) = await CreatePublishedEventAsync(service, fixture, capacity: 1);
        var first = await service.RegisterAsync(
            fixture.Member.Id,
            eventId,
            positionId,
            "registration-first",
            CancellationToken.None);

        await Assert.ThrowsAsync<EventConflictException>(() => service.RegisterAsync(
            fixture.AdditionalMember.Id,
            eventId,
            positionId,
            "registration-capacity",
            CancellationToken.None));

        await service.RejectRegistrationAsync(
            fixture.Core.Id,
            eventId,
            first.Id,
            "registration-reject",
            CancellationToken.None);
        var second = await service.RegisterAsync(
            fixture.AdditionalMember.Id,
            eventId,
            positionId,
            "registration-second",
            CancellationToken.None);

        Assert.Equal(EventRegistrationStatus.Pending, second.Status);
        Assert.Contains(fixture.EventStore.AuditLogs, audit => audit.Action == "EventRegistrationRejected");
    }

    [Fact]
    public async Task DirectAssignmentBypassesDepartmentButNotMultiplePositionPolicy()
    {
        var fixture = CreateFixture();
        var service = new EventManagementService(fixture.EventStore, fixture.MemberStore);
        var (eventId, firstPositionId) = await CreatePublishedEventAsync(service, fixture, requiredDepartmentId: Department.CoreDepartmentId);
        var secondPosition = await service.AddPositionAsync(
            fixture.Core.Id,
            eventId,
            new CreateEventPositionCommand("Media", null, 2, null, 1),
            "event-position-second",
            CancellationToken.None);

        var assignment = await service.AssignAsync(
            fixture.Core.Id,
            eventId,
            firstPositionId,
            fixture.Member.Id,
            "assignment-first",
            CancellationToken.None);

        Assert.Equal(EventRegistrationStatus.Assigned, assignment.Status);
        await Assert.ThrowsAsync<EventConflictException>(() => service.AssignAsync(
            fixture.Core.Id,
            eventId,
            secondPosition.Id,
            fixture.Member.Id,
            "assignment-multiple",
            CancellationToken.None));
    }

    [Fact]
    public async Task CoreCanCheckInUnregisteredMemberDuringOrAfterEventAndGetHistory()
    {
        var fixture = CreateFixture();
        var service = new EventManagementService(fixture.EventStore, fixture.MemberStore);
        var (eventId, _) = await CreatePublishedEventAsync(service, fixture);
        await service.CloseRegistrationAsync(fixture.Core.Id, eventId, "event-close", CancellationToken.None);
        await service.StartAsync(fixture.Core.Id, eventId, "event-start", CancellationToken.None);

        var attendance = await service.CheckInAsync(
            fixture.Core.Id,
            eventId,
            fixture.Member.Id,
            "attendance-check-in",
            CancellationToken.None);
        var repeated = await service.CheckInAsync(
            fixture.Core.Id,
            eventId,
            fixture.Member.Id,
            "attendance-repeat",
            CancellationToken.None);
        await service.CompleteAsync(fixture.Core.Id, eventId, "event-complete", CancellationToken.None);
        var afterCompletion = await service.CheckInAsync(
            fixture.Core.Id,
            eventId,
            fixture.AdditionalMember.Id,
            "attendance-after-complete",
            CancellationToken.None);

        Assert.Equal(attendance.Id, repeated.Id);
        Assert.NotEqual(attendance.Id, afterCompletion.Id);
        Assert.Equal(2, (await service.ListAttendanceAsync(fixture.Core.Id, eventId, CancellationToken.None)).Count);
        Assert.Equal(2, fixture.EventStore.AuditLogs.Count(audit => audit.Action == "EventMemberCheckedIn"));
        Assert.Single(await service.ListMemberHistoryAsync(fixture.Member.Id, fixture.Member.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CheckInRejectsNonManagerInactiveMemberAndEventsOutsideAttendanceLifecycle()
    {
        var fixture = CreateFixture();
        var service = new EventManagementService(fixture.EventStore, fixture.MemberStore);
        var (eventId, _) = await CreatePublishedEventAsync(service, fixture);

        await Assert.ThrowsAsync<EventAccessDeniedException>(() => service.CheckInAsync(
            fixture.Member.Id, eventId, fixture.Member.Id, "attendance-member", CancellationToken.None));
        await Assert.ThrowsAsync<EventValidationException>(() => service.CheckInAsync(
            fixture.Core.Id, eventId, fixture.Member.Id, "attendance-published", CancellationToken.None));

        fixture.AdditionalMember.Deactivate();
        await service.CloseRegistrationAsync(fixture.Core.Id, eventId, "event-close", CancellationToken.None);
        await service.StartAsync(fixture.Core.Id, eventId, "event-start", CancellationToken.None);
        await Assert.ThrowsAsync<EventValidationException>(() => service.CheckInAsync(
            fixture.Core.Id, eventId, fixture.AdditionalMember.Id, "attendance-inactive", CancellationToken.None));

        var (cancelledEventId, _) = await CreatePublishedEventAsync(service, fixture);
        await service.CancelAsync(fixture.Core.Id, cancelledEventId, "event-cancel", CancellationToken.None);
        await Assert.ThrowsAsync<EventValidationException>(() => service.CheckInAsync(
            fixture.Core.Id, cancelledEventId, fixture.Member.Id, "attendance-cancelled", CancellationToken.None));
    }

    [Fact]
    public async Task MemberCannotReadAnotherMembersEventHistory()
    {
        var fixture = CreateFixture();
        var service = new EventManagementService(fixture.EventStore, fixture.MemberStore);

        await Assert.ThrowsAsync<EventAccessDeniedException>(() => service.ListMemberHistoryAsync(
            fixture.Member.Id,
            fixture.AdditionalMember.Id,
            CancellationToken.None));
    }

    private static async Task<(Guid EventId, Guid PositionId)> CreatePublishedEventAsync(
        EventManagementService service,
        Fixture fixture,
        int capacity = 2,
        Guid? requiredDepartmentId = null)
    {
        var @event = await service.CreateAsync(
            fixture.Core.Id,
            new CreateEventCommand("Event", null, null, DateTimeOffset.UtcNow.AddDays(1), null),
            "event-create",
            CancellationToken.None);
        var position = await service.AddPositionAsync(
            fixture.Core.Id,
            @event.Id,
            new CreateEventPositionCommand("Position", null, capacity, requiredDepartmentId ?? fixture.Department.Id, 0),
            "event-position",
            CancellationToken.None);
        await service.PublishAsync(fixture.Core.Id, @event.Id, "event-publish", CancellationToken.None);
        return (@event.Id, position.Id);
    }

    private static Fixture CreateFixture()
    {
        var department = Department.CreateRegular("Operations", "operations");
        var generation = Generation.Create("K26", "K26");
        var memberStore = new FakeMemberStore();
        memberStore.Departments.AddRange([Department.CreateCore(), department]);
        var core = Member.Create("CORE001", "Core User", "core@example.com", Department.CoreDepartmentId, true, generation.Id, MemberPosition.Core);
        var member = Member.Create("MEMBER001", "Member User", "member@example.com", department.Id, false, generation.Id, MemberPosition.Member);
        var additionalMember = Member.Create("MEMBER002", "Second User", "second@example.com", department.Id, false, generation.Id, MemberPosition.Member);
        memberStore.Members.AddRange([core, member, additionalMember]);

        return new Fixture(memberStore, new FakeEventStore(), core, member, additionalMember, department);
    }

    private sealed record Fixture(
        FakeMemberStore MemberStore,
        FakeEventStore EventStore,
        Member Core,
        Member Member,
        Member AdditionalMember,
        Department Department);

    private sealed class FakeEventStore : IEventStore
    {
        public List<Event> Events { get; } = [];

        public List<EventPosition> Positions { get; } = [];

        public List<EventRegistration> Registrations { get; } = [];

        public List<EventAttendance> Attendances { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public Task<IReadOnlyList<EventView>> ListViewsAsync(bool includeUnpublished, CancellationToken cancellationToken)
        {
            var events = includeUnpublished
                ? Events
                : Events.Where(@event => @event.Status is not (EventStatus.Draft or EventStatus.Cancelled)).ToList();
            return Task.FromResult<IReadOnlyList<EventView>>(events.Select(ToView).ToArray());
        }

        public Task<EventView?> FindViewAsync(Guid eventId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Events.SingleOrDefault(@event => @event.Id == eventId) is { } @event
                ? (EventView?)ToView(@event)
                : null);
        }

        public Task<Event?> FindEventAsync(Guid eventId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Events.SingleOrDefault(@event => @event.Id == eventId));
        }

        public Task<EventPosition?> FindPositionAsync(Guid eventId, Guid positionId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Positions.SingleOrDefault(position => position.EventId == eventId && position.Id == positionId));
        }

        public Task<int> CountPositionsAsync(Guid eventId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Positions.Count(position => position.EventId == eventId));
        }

        public Task<EventRegistration?> FindRegistrationAsync(Guid eventId, Guid registrationId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Registrations.SingleOrDefault(registration =>
                registration.Id == registrationId
                && Positions.Any(position => position.Id == registration.EventPositionId && position.EventId == eventId)));
        }

        public Task<EventRegistration?> FindActiveRegistrationAsync(Guid eventPositionId, Guid memberId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Registrations.SingleOrDefault(registration =>
                registration.EventPositionId == eventPositionId
                && registration.MemberId == memberId
                && registration.ConsumesCapacity));
        }

        public Task<int> CountCapacityConsumingRegistrationsAsync(Guid eventPositionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Registrations.Count(registration =>
                registration.EventPositionId == eventPositionId
                && registration.ConsumesCapacity));
        }

        public Task<bool> HasActiveRegistrationForEventAsync(Guid eventId, Guid memberId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Registrations.Any(registration =>
                registration.MemberId == memberId
                && registration.ConsumesCapacity
                && Positions.Any(position => position.Id == registration.EventPositionId && position.EventId == eventId)));
        }

        public Task<IReadOnlyList<EventRegistrationView>> ListRegistrationViewsAsync(Guid eventId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EventRegistrationView>>(Registrations
                .Where(registration => Positions.Any(position => position.Id == registration.EventPositionId && position.EventId == eventId))
                .Select(registration => new EventRegistrationView(
                    registration.Id,
                    eventId,
                    registration.EventPositionId,
                    registration.MemberId,
                    registration.Status,
                    registration.RequestedAt,
                    registration.DecidedAt,
                    registration.DecidedByMemberId,
                    registration.AssignedAt,
                    registration.AssignedByMemberId,
                    registration.CancelledAt,
                    registration.CreatedAt,
                    registration.UpdatedAt))
                .ToArray());
        }

        public Task<EventAttendance?> FindAttendanceAsync(Guid eventId, Guid memberId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Attendances.SingleOrDefault(attendance => attendance.EventId == eventId && attendance.MemberId == memberId));
        }

        public Task<IReadOnlyList<EventAttendanceView>> ListAttendanceViewsAsync(Guid eventId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EventAttendanceView>>(Attendances
                .Where(attendance => attendance.EventId == eventId)
                .Select(ToView)
                .ToArray());
        }

        public Task<IReadOnlyList<MemberEventHistoryView>> ListMemberEventHistoryViewsAsync(Guid memberId, CancellationToken cancellationToken)
        {
            var eventIds = Registrations
                .Where(registration => registration.MemberId == memberId)
                .Select(registration => Positions.Single(position => position.Id == registration.EventPositionId).EventId)
                .Concat(Attendances.Where(attendance => attendance.MemberId == memberId).Select(attendance => attendance.EventId))
                .Distinct()
                .ToArray();
            return Task.FromResult<IReadOnlyList<MemberEventHistoryView>>(Events
                .Where(@event => eventIds.Contains(@event.Id))
                .Select(@event => new MemberEventHistoryView(
                    @event.Id,
                    @event.Name,
                    @event.StartsAt,
                    @event.Status,
                    Registrations
                        .Where(registration => registration.MemberId == memberId && Positions.Single(position => position.Id == registration.EventPositionId).EventId == @event.Id)
                        .Select(registration =>
                        {
                            var position = Positions.Single(candidate => candidate.Id == registration.EventPositionId);
                            return new MemberEventRegistrationHistoryView(
                                registration.Id,
                                position.Id,
                                position.Name,
                                registration.Status,
                                registration.RequestedAt,
                                registration.DecidedAt,
                                registration.AssignedAt,
                                registration.CancelledAt);
                        })
                        .ToArray(),
                    Attendances.SingleOrDefault(attendance => attendance.EventId == @event.Id && attendance.MemberId == memberId) is { } attendance
                        ? ToView(attendance)
                        : null))
                .ToArray());
        }

        public Task AddEventAsync(Event eventItem, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Events.Add(eventItem);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task UpdateEventAsync(Event eventItem, AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddPositionAsync(EventPosition position, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Positions.Add(position);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task UpdatePositionAsync(EventPosition position, Event eventItem, AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveRegistrationAsync(EventRegistration registration, bool isNew, Event eventItem, AuditLog auditLog, CancellationToken cancellationToken)
        {
            if (isNew)
            {
                Registrations.Add(registration);
            }

            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddAttendanceAsync(EventAttendance attendance, Event eventItem, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Attendances.Add(attendance);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        private EventView ToView(Event @event)
        {
            return new EventView(
                @event.Id,
                @event.Name,
                @event.Description,
                @event.Location,
                @event.StartsAt,
                @event.EndsAt,
                @event.Status,
                @event.AllowMultiplePositions,
                @event.CreatedByMemberId,
                Positions.Where(position => position.EventId == @event.Id).Select(ToView).ToArray(),
                @event.CreatedAt,
                @event.UpdatedAt);
        }

        private static EventPositionView ToView(EventPosition position)
        {
            return new EventPositionView(
                position.Id,
                position.EventId,
                position.Name,
                position.Description,
                position.Capacity,
                position.RequiredDepartmentId,
                position.SortOrder,
                position.CreatedAt,
                position.UpdatedAt);
        }

        private static EventAttendanceView ToView(EventAttendance attendance)
        {
            return new EventAttendanceView(
                attendance.Id,
                attendance.EventId,
                attendance.MemberId,
                attendance.CheckedInAt,
                attendance.CheckedInByMemberId,
                attendance.CreatedAt);
        }
    }

    private sealed class FakeMemberStore : IMemberStore
    {
        public List<Member> Members { get; } = [];

        public List<Department> Departments { get; } = [];

        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Members.SingleOrDefault(member => member.Id == memberId));
        }

        public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(int page, int pageSize, MemberStatus? status, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(IReadOnlyCollection<string> studentIds, IReadOnlyCollection<string> clubEmails, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Departments.SingleOrDefault(department => department.Id == departmentId));
        }

        public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task AddRangeAsync(IReadOnlyCollection<MemberAuditEntry> entries, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
