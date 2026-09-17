using System.Text.Json;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Events;
using Felion.Domain.Members;

namespace Felion.Application.Events;

public sealed class EventManagementService(
    IEventStore store,
    IMemberStore memberStore) : IEventManagementService
{
    public async Task<IReadOnlyList<EventDto>> ListAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await GetActiveActorAsync(actorMemberId, cancellationToken);
        var includeUnpublished = actor.Position is MemberPosition.Admin or MemberPosition.Core;
        var events = await store.ListViewsAsync(includeUnpublished, cancellationToken);
        return events.Select(ToDto).ToArray();
    }

    public async Task<EventDto> GetAsync(Guid actorMemberId, Guid eventId, CancellationToken cancellationToken)
    {
        var actor = await GetActiveActorAsync(actorMemberId, cancellationToken);
        var @event = await store.FindViewAsync(eventId, cancellationToken)
            ?? throw new EventNotFoundException(eventId);
        if (actor.Position is not (MemberPosition.Admin or MemberPosition.Core)
            && @event.Status is EventStatus.Draft or EventStatus.Cancelled)
        {
            throw new EventNotFoundException(eventId);
        }

        return ToDto(@event);
    }

    public async Task<EventDto> CreateAsync(
        Guid actorMemberId,
        CreateEventCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        Event @event;
        try
        {
            @event = Event.Create(
                command.Name,
                command.Description,
                command.Location,
                command.StartsAt,
                command.EndsAt,
                command.AllowMultiplePositions,
                actorMemberId);
        }
        catch (DomainException exception)
        {
            throw new EventValidationException(exception.Message);
        }

        await store.AddEventAsync(@event, CreateAudit(actorMemberId, "EventCreated", @event.Id, correlationId, after: Snapshot(@event)), cancellationToken);
        return ToDto(new EventView(
            @event.Id,
            @event.Name,
            @event.Description,
            @event.Location,
            @event.StartsAt,
            @event.EndsAt,
            @event.Status,
            @event.AllowMultiplePositions,
            @event.CreatedByMemberId,
            [],
            @event.CreatedAt,
            @event.UpdatedAt));
    }

    public async Task<EventDto> UpdateAsync(
        Guid actorMemberId,
        Guid eventId,
        UpdateEventCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        if (command.Name is null
            && command.Description is null
            && command.Location is null
            && command.StartsAt is null
            && command.EndsAt is null
            && command.AllowMultiplePositions is null)
        {
            throw new EventValidationException("At least one event field must be provided.");
        }

        var @event = await FindManagedEventAsync(eventId, cancellationToken);
        var before = Snapshot(@event);
        try
        {
            @event.Update(
                command.Name,
                command.Description,
                command.Location,
                command.StartsAt,
                command.EndsAt,
                command.AllowMultiplePositions);
        }
        catch (DomainException exception)
        {
            throw new EventValidationException(exception.Message);
        }

        await store.UpdateEventAsync(@event, CreateAudit(actorMemberId, "EventUpdated", @event.Id, correlationId, before, Snapshot(@event)), cancellationToken);
        return await GetManagedViewAsync(@event.Id, cancellationToken);
    }

    public Task<EventDto> PublishAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken)
    {
        return TransitionAsync(actorMemberId, eventId, "EventPublished", correlationId, async (@event, cancellationToken) =>
        {
            var hasPositions = await store.CountPositionsAsync(@event.Id, cancellationToken) > 0;
            @event.Publish(hasPositions);
        }, cancellationToken);
    }

    public Task<EventDto> CloseRegistrationAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken)
    {
        return TransitionAsync(actorMemberId, eventId, "EventRegistrationClosed", correlationId, (@event, _) =>
        {
            @event.CloseRegistration();
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<EventDto> StartAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken)
    {
        return TransitionAsync(actorMemberId, eventId, "EventStarted", correlationId, (@event, _) =>
        {
            @event.Start();
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<EventDto> CompleteAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken)
    {
        return TransitionAsync(actorMemberId, eventId, "EventCompleted", correlationId, (@event, _) =>
        {
            @event.Complete();
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<EventDto> CancelAsync(Guid actorMemberId, Guid eventId, string correlationId, CancellationToken cancellationToken)
    {
        return TransitionAsync(actorMemberId, eventId, "EventCancelled", correlationId, (@event, _) =>
        {
            @event.Cancel();
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public async Task<EventPositionDto> AddPositionAsync(
        Guid actorMemberId,
        Guid eventId,
        CreateEventPositionCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        var @event = await FindManagedEventAsync(eventId, cancellationToken);
        EnsurePositionMutable(@event);
        await ValidateRequiredDepartmentAsync(command.RequiredDepartmentId, cancellationToken);

        EventPosition position;
        try
        {
            position = EventPosition.Create(
                eventId,
                command.Name,
                command.Description,
                command.Capacity,
                command.RequiredDepartmentId,
                command.SortOrder);
        }
        catch (DomainException exception)
        {
            throw new EventValidationException(exception.Message);
        }

        await store.AddPositionAsync(position, CreateAudit(actorMemberId, "EventPositionCreated", position.Id, correlationId, after: Snapshot(position), entityType: "EventPosition"), cancellationToken);
        return ToDto(ToView(position));
    }

    public async Task<EventPositionDto> UpdatePositionAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid positionId,
        UpdateEventPositionCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        if (command.Name is null
            && command.Description is null
            && command.Capacity is null
            && command.RequiredDepartmentId is null
            && command.SortOrder is null)
        {
            throw new EventValidationException("At least one event position field must be provided.");
        }

        var @event = await FindManagedEventAsync(eventId, cancellationToken);
        EnsurePositionMutable(@event);
        await ValidateRequiredDepartmentAsync(command.RequiredDepartmentId, cancellationToken);
        var position = await store.FindPositionAsync(eventId, positionId, track: true, cancellationToken)
            ?? throw new EventPositionNotFoundException(eventId, positionId);
        var activeRegistrations = await store.CountCapacityConsumingRegistrationsAsync(position.Id, cancellationToken);
        if (command.Capacity is not null && command.Capacity.Value < activeRegistrations)
        {
            throw new EventValidationException("Event position capacity cannot be below current active registrations.");
        }

        if (command.RequiredDepartmentId is not null
            && command.RequiredDepartmentId != position.RequiredDepartmentId
            && activeRegistrations > 0)
        {
            throw new EventValidationException("Required department cannot change while active registrations exist.");
        }

        var before = Snapshot(position);
        try
        {
            position.Update(
                command.Name,
                command.Description,
                command.Capacity,
                command.RequiredDepartmentId,
                command.SortOrder);
        }
        catch (DomainException exception)
        {
            throw new EventValidationException(exception.Message);
        }

        @event.RecordRegistrationChange();
        await store.UpdatePositionAsync(
            position,
            @event,
            CreateAudit(actorMemberId, "EventPositionUpdated", position.Id, correlationId, before, Snapshot(position), "EventPosition"),
            cancellationToken);
        return ToDto(ToView(position));
    }

    public async Task<EventRegistrationDto> RegisterAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid positionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var member = await GetActiveActorAsync(actorMemberId, cancellationToken);
        var @event = await store.FindEventAsync(eventId, track: true, cancellationToken)
            ?? throw new EventNotFoundException(eventId);
        if (@event.Status != EventStatus.Published)
        {
            throw new EventValidationException("Registrations are open only for published events.");
        }

        var position = await store.FindPositionAsync(eventId, positionId, track: true, cancellationToken)
            ?? throw new EventPositionNotFoundException(eventId, positionId);
        if (position.RequiredDepartmentId is not null && position.RequiredDepartmentId != member.DepartmentId)
        {
            throw new EventValidationException("Member does not meet this position's department requirement.");
        }

        await EnsureRegistrationAllowedAsync(@event, position, member.Id, cancellationToken);
        var registration = EventRegistration.Request(position.Id, member.Id);
        @event.RecordRegistrationChange();
        await store.SaveRegistrationAsync(
            registration,
            isNew: true,
            @event,
            CreateAudit(member.Id, "EventRegistrationRequested", registration.Id, correlationId, after: Snapshot(registration), entityType: "EventRegistration"),
            cancellationToken);
        return ToDto(registration);
    }

    public async Task<IReadOnlyList<EventRegistrationDto>> ListRegistrationsAsync(
        Guid actorMemberId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        _ = await FindManagedEventAsync(eventId, cancellationToken);
        var registrations = await store.ListRegistrationViewsAsync(eventId, cancellationToken);
        return registrations.Select(ToDto).ToArray();
    }

    public Task<EventRegistrationDto> ApproveRegistrationAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid registrationId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        return DecideRegistrationAsync(
            actorMemberId,
            eventId,
            registrationId,
            "EventRegistrationApproved",
            (registration, actorId) => registration.Approve(actorId),
            correlationId,
            cancellationToken);
    }

    public Task<EventRegistrationDto> RejectRegistrationAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid registrationId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        return DecideRegistrationAsync(
            actorMemberId,
            eventId,
            registrationId,
            "EventRegistrationRejected",
            (registration, actorId) => registration.Reject(actorId),
            correlationId,
            cancellationToken);
    }

    public async Task<EventRegistrationDto> AssignAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid positionId,
        Guid memberId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        var member = await memberStore.FindByIdAsync(memberId, track: false, cancellationToken);
        if (member is null || member.Status != MemberStatus.Active)
        {
            throw new EventValidationException("Only an active Member can be assigned.");
        }

        var @event = await FindManagedEventAsync(eventId, cancellationToken);
        if (@event.Status is not (EventStatus.Published or EventStatus.RegistrationClosed))
        {
            throw new EventValidationException("Assignments are available only before an event starts.");
        }

        var position = await store.FindPositionAsync(eventId, positionId, track: true, cancellationToken)
            ?? throw new EventPositionNotFoundException(eventId, positionId);
        await EnsureRegistrationAllowedAsync(@event, position, member.Id, cancellationToken);
        var registration = EventRegistration.Assign(position.Id, member.Id, actorMemberId);
        @event.RecordRegistrationChange();
        await store.SaveRegistrationAsync(
            registration,
            isNew: true,
            @event,
            CreateAudit(actorMemberId, "EventMemberAssigned", registration.Id, correlationId, after: Snapshot(registration), entityType: "EventRegistration"),
            cancellationToken);
        return ToDto(registration);
    }

    public async Task<EventAttendanceDto> CheckInAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid memberId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        var member = await memberStore.FindByIdAsync(memberId, track: false, cancellationToken);
        if (member is null || member.Status != MemberStatus.Active)
        {
            throw new EventValidationException("Only an active Member can be checked in.");
        }

        var @event = await FindManagedEventAsync(eventId, cancellationToken);
        if (@event.Status is not (EventStatus.InProgress or EventStatus.Completed))
        {
            throw new EventValidationException("Check-in is available only while an event is in progress or completed.");
        }

        var existing = await store.FindAttendanceAsync(eventId, memberId, track: false, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        EventAttendance attendance;
        try
        {
            attendance = EventAttendance.CheckIn(eventId, memberId, actorMemberId);
        }
        catch (DomainException exception)
        {
            throw new EventValidationException(exception.Message);
        }

        @event.RecordAttendanceChange();
        try
        {
            await store.AddAttendanceAsync(
                attendance,
                @event,
                CreateAudit(actorMemberId, "EventMemberCheckedIn", attendance.Id, correlationId, after: Snapshot(attendance), entityType: "EventAttendance"),
                cancellationToken);
        }
        catch (EventConflictException)
        {
            var concurrentlyCreated = await store.FindAttendanceAsync(eventId, memberId, track: false, cancellationToken);
            if (concurrentlyCreated is not null)
            {
                return ToDto(concurrentlyCreated);
            }

            throw;
        }

        return ToDto(attendance);
    }

    public async Task<IReadOnlyList<EventAttendanceDto>> ListAttendanceAsync(
        Guid actorMemberId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        _ = await FindManagedEventAsync(eventId, cancellationToken);
        var attendance = await store.ListAttendanceViewsAsync(eventId, cancellationToken);
        return attendance.Select(ToDto).ToArray();
    }

    public async Task<IReadOnlyList<MemberEventHistoryDto>> ListMemberHistoryAsync(
        Guid actorMemberId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var actor = await GetActiveActorAsync(actorMemberId, cancellationToken);
        if (actor.Id != memberId && actor.Position is not (MemberPosition.Admin or MemberPosition.Core))
        {
            throw new EventAccessDeniedException();
        }

        if (await memberStore.FindByIdAsync(memberId, track: false, cancellationToken) is null)
        {
            throw new EventMemberNotFoundException(memberId);
        }

        var history = await store.ListMemberEventHistoryViewsAsync(memberId, cancellationToken);
        return history.Select(ToDto).ToArray();
    }

    private async Task<EventDto> TransitionAsync(
        Guid actorMemberId,
        Guid eventId,
        string auditAction,
        string correlationId,
        Func<Event, CancellationToken, Task> transition,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        var @event = await FindManagedEventAsync(eventId, cancellationToken);
        var before = Snapshot(@event);
        try
        {
            await transition(@event, cancellationToken);
        }
        catch (DomainException exception)
        {
            throw new EventValidationException(exception.Message);
        }

        await store.UpdateEventAsync(@event, CreateAudit(actorMemberId, auditAction, @event.Id, correlationId, before, Snapshot(@event)), cancellationToken);
        return await GetManagedViewAsync(@event.Id, cancellationToken);
    }

    private async Task<EventRegistrationDto> DecideRegistrationAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid registrationId,
        string auditAction,
        Action<EventRegistration, Guid> decision,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(actorMemberId, cancellationToken);
        var @event = await FindManagedEventAsync(eventId, cancellationToken);
        if (@event.Status is not (EventStatus.Published or EventStatus.RegistrationClosed))
        {
            throw new EventValidationException("Registration decisions are available only before an event starts.");
        }

        var registration = await store.FindRegistrationAsync(eventId, registrationId, track: true, cancellationToken)
            ?? throw new EventRegistrationNotFoundException(eventId, registrationId);
        var before = Snapshot(registration);
        try
        {
            decision(registration, actorMemberId);
        }
        catch (DomainException exception)
        {
            throw new EventValidationException(exception.Message);
        }

        @event.RecordRegistrationChange();
        await store.SaveRegistrationAsync(
            registration,
            isNew: false,
            @event,
            CreateAudit(actorMemberId, auditAction, registration.Id, correlationId, before, Snapshot(registration), "EventRegistration"),
            cancellationToken);
        return ToDto(registration);
    }

    private async Task<Member> GetActiveActorAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null || actor.Status != MemberStatus.Active)
        {
            throw new EventAccessDeniedException();
        }

        return actor;
    }

    private async Task EnsureManagerAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await GetActiveActorAsync(actorMemberId, cancellationToken);
        if (actor.Position is not (MemberPosition.Admin or MemberPosition.Core))
        {
            throw new EventAccessDeniedException();
        }
    }

    private async Task<Event> FindManagedEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return await store.FindEventAsync(eventId, track: true, cancellationToken)
            ?? throw new EventNotFoundException(eventId);
    }

    private async Task<EventDto> GetManagedViewAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var view = await store.FindViewAsync(eventId, cancellationToken)
            ?? throw new EventNotFoundException(eventId);
        return ToDto(view);
    }

    private async Task ValidateRequiredDepartmentAsync(Guid? departmentId, CancellationToken cancellationToken)
    {
        if (departmentId is null)
        {
            return;
        }

        var department = await memberStore.FindDepartmentAsync(departmentId.Value, cancellationToken);
        if (department is null || !department.IsActive)
        {
            throw new EventValidationException("Required department does not exist or is inactive.");
        }
    }

    private async Task EnsureRegistrationAllowedAsync(
        Event @event,
        EventPosition position,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        if (await store.FindActiveRegistrationAsync(position.Id, memberId, cancellationToken) is not null)
        {
            throw new EventConflictException("Member already has an active registration for this position.");
        }

        if (!@event.AllowMultiplePositions
            && await store.HasActiveRegistrationForEventAsync(@event.Id, memberId, cancellationToken))
        {
            throw new EventConflictException("Member can hold only one active event position.");
        }

        var activeRegistrations = await store.CountCapacityConsumingRegistrationsAsync(position.Id, cancellationToken);
        if (activeRegistrations >= position.Capacity)
        {
            throw new EventConflictException("Event position capacity has been reached.");
        }
    }

    private static void EnsurePositionMutable(Event @event)
    {
        if (@event.Status is EventStatus.Completed or EventStatus.Cancelled or EventStatus.InProgress)
        {
            throw new EventValidationException("Positions cannot be changed after an event has started or been closed.");
        }
    }

    private static AuditLog CreateAudit(
        Guid actorMemberId,
        string action,
        Guid entityId,
        string correlationId,
        string? before = null,
        string? after = null,
        string entityType = "Event")
    {
        return AuditLog.Create(
            AuditActorType.WebMember,
            actorMemberId,
            null,
            action,
            entityType,
            entityId,
            correlationId,
            beforeJson: before,
            afterJson: after);
    }

    private static EventDto ToDto(EventView @event)
    {
        return new EventDto(
            @event.Id,
            @event.Name,
            @event.Description,
            @event.Location,
            @event.StartsAt,
            @event.EndsAt,
            @event.Status,
            @event.AllowMultiplePositions,
            @event.CreatedByMemberId,
            @event.Positions.Select(ToDto).ToArray(),
            @event.CreatedAt,
            @event.UpdatedAt);
    }

    private static EventPositionDto ToDto(EventPositionView position)
    {
        return new EventPositionDto(
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

    private static string Snapshot(Event @event)
    {
        return JsonSerializer.Serialize(new
        {
            @event.Id,
            @event.Name,
            @event.Description,
            @event.Location,
            @event.StartsAt,
            @event.EndsAt,
            @event.Status,
            @event.AllowMultiplePositions,
            @event.CreatedByMemberId,
            @event.UpdatedAt
        });
    }

    private static string Snapshot(EventPosition position)
    {
        return JsonSerializer.Serialize(new
        {
            position.Id,
            position.EventId,
            position.Name,
            position.Description,
            position.Capacity,
            position.RequiredDepartmentId,
            position.SortOrder,
            position.UpdatedAt
        });
    }

    private static string Snapshot(EventRegistration registration)
    {
        return JsonSerializer.Serialize(new
        {
            registration.Id,
            registration.EventPositionId,
            registration.MemberId,
            registration.Status,
            registration.RequestedAt,
            registration.DecidedAt,
            registration.DecidedByMemberId,
            registration.AssignedAt,
            registration.AssignedByMemberId,
            registration.CancelledAt,
            registration.UpdatedAt
        });
    }

    private static string Snapshot(EventAttendance attendance)
    {
        return JsonSerializer.Serialize(new
        {
            attendance.Id,
            attendance.EventId,
            attendance.MemberId,
            attendance.CheckedInAt,
            attendance.CheckedInByMemberId,
            attendance.CreatedAt
        });
    }

    private static EventRegistrationDto ToDto(EventRegistration registration)
    {
        return new EventRegistrationDto(
            registration.Id,
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
            registration.UpdatedAt);
    }

    private static EventRegistrationDto ToDto(EventRegistrationView registration)
    {
        return new EventRegistrationDto(
            registration.Id,
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
            registration.UpdatedAt);
    }

    private static EventAttendanceDto ToDto(EventAttendance attendance)
    {
        return new EventAttendanceDto(
            attendance.Id,
            attendance.EventId,
            attendance.MemberId,
            attendance.CheckedInAt,
            attendance.CheckedInByMemberId,
            attendance.CreatedAt);
    }

    private static EventAttendanceDto ToDto(EventAttendanceView attendance)
    {
        return new EventAttendanceDto(
            attendance.Id,
            attendance.EventId,
            attendance.MemberId,
            attendance.CheckedInAt,
            attendance.CheckedInByMemberId,
            attendance.CreatedAt);
    }

    private static MemberEventHistoryDto ToDto(MemberEventHistoryView history)
    {
        return new MemberEventHistoryDto(
            history.EventId,
            history.EventName,
            history.StartsAt,
            history.EventStatus,
            history.Registrations.Select(registration => new MemberEventRegistrationHistoryDto(
                registration.Id,
                registration.EventPositionId,
                registration.EventPositionName,
                registration.Status,
                registration.RequestedAt,
                registration.DecidedAt,
                registration.AssignedAt,
                registration.CancelledAt)).ToArray(),
            history.Attendance is null ? null : ToDto(history.Attendance));
    }
}
