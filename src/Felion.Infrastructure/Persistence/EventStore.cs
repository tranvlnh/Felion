using Felion.Application.Events;
using Felion.Domain.Audit;
using Felion.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class EventStore(FelionDbContext dbContext) : IEventStore
{
    public async Task<IReadOnlyList<EventView>> ListViewsAsync(
        bool includeUnpublished,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Events.AsNoTracking().AsQueryable();
        if (!includeUnpublished)
        {
            query = query.Where(@event => @event.Status != EventStatus.Draft && @event.Status != EventStatus.Cancelled);
        }

        var events = await query
            .OrderBy(@event => @event.StartsAt)
            .ToListAsync(cancellationToken);
        return await CreateViewsAsync(events, cancellationToken);
    }

    public async Task<EventView?> FindViewAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        return @event is null ? null : (await CreateViewsAsync([@event], cancellationToken))[0];
    }

    public Task<Event?> FindEventAsync(Guid eventId, bool track, CancellationToken cancellationToken)
    {
        var query = dbContext.Events.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(@event => @event.Id == eventId, cancellationToken);
    }

    public Task<EventPosition?> FindPositionAsync(
        Guid eventId,
        Guid positionId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EventPositions.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            position => position.EventId == eventId && position.Id == positionId,
            cancellationToken);
    }

    public Task<int> CountPositionsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return dbContext.EventPositions.CountAsync(position => position.EventId == eventId, cancellationToken);
    }

    public Task<EventRegistration?> FindRegistrationAsync(
        Guid eventId,
        Guid registrationId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EventRegistrations
            .Where(registration => registration.Id == registrationId)
            .Join(
                dbContext.EventPositions,
                registration => registration.EventPositionId,
                position => position.Id,
                (registration, position) => new { registration, position.EventId })
            .Where(candidate => candidate.EventId == eventId)
            .Select(candidate => candidate.registration);
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(cancellationToken);
    }

    public Task<EventRegistration?> FindActiveRegistrationAsync(
        Guid eventPositionId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        return dbContext.EventRegistrations.SingleOrDefaultAsync(
            registration => registration.EventPositionId == eventPositionId
                && registration.MemberId == memberId
                && (registration.Status == EventRegistrationStatus.Pending
                    || registration.Status == EventRegistrationStatus.Approved
                    || registration.Status == EventRegistrationStatus.Assigned),
            cancellationToken);
    }

    public Task<int> CountCapacityConsumingRegistrationsAsync(
        Guid eventPositionId,
        CancellationToken cancellationToken)
    {
        return dbContext.EventRegistrations.CountAsync(
            registration => registration.EventPositionId == eventPositionId
                && (registration.Status == EventRegistrationStatus.Pending
                    || registration.Status == EventRegistrationStatus.Approved
                    || registration.Status == EventRegistrationStatus.Assigned),
            cancellationToken);
    }

    public Task<bool> HasActiveRegistrationForEventAsync(
        Guid eventId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        return dbContext.EventRegistrations.AnyAsync(
            registration => registration.MemberId == memberId
                && (registration.Status == EventRegistrationStatus.Pending
                    || registration.Status == EventRegistrationStatus.Approved
                    || registration.Status == EventRegistrationStatus.Assigned)
                && dbContext.EventPositions.Any(position => position.Id == registration.EventPositionId && position.EventId == eventId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<EventRegistrationView>> ListRegistrationViewsAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return await dbContext.EventRegistrations
            .AsNoTracking()
            .Join(
                dbContext.EventPositions,
                registration => registration.EventPositionId,
                position => position.Id,
                (registration, position) => new { registration, position.EventId })
            .Where(candidate => candidate.EventId == eventId)
            .OrderBy(candidate => candidate.registration.CreatedAt)
            .Select(candidate => new EventRegistrationView(
                candidate.registration.Id,
                candidate.EventId,
                candidate.registration.EventPositionId,
                candidate.registration.MemberId,
                candidate.registration.Status,
                candidate.registration.RequestedAt,
                candidate.registration.DecidedAt,
                candidate.registration.DecidedByMemberId,
                candidate.registration.AssignedAt,
                candidate.registration.AssignedByMemberId,
                candidate.registration.CancelledAt,
                candidate.registration.CreatedAt,
                candidate.registration.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<EventAttendance?> FindAttendanceAsync(
        Guid eventId,
        Guid memberId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EventAttendances.AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            attendance => attendance.EventId == eventId && attendance.MemberId == memberId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<EventAttendanceView>> ListAttendanceViewsAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return await dbContext.EventAttendances
            .AsNoTracking()
            .Where(attendance => attendance.EventId == eventId)
            .OrderByDescending(attendance => attendance.CheckedInAt)
            .Select(attendance => new EventAttendanceView(
                attendance.Id,
                attendance.EventId,
                attendance.MemberId,
                attendance.CheckedInAt,
                attendance.CheckedInByMemberId,
                attendance.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberEventHistoryView>> ListMemberEventHistoryViewsAsync(
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var registrations = await dbContext.EventRegistrations
            .AsNoTracking()
            .Where(registration => registration.MemberId == memberId)
            .Join(
                dbContext.EventPositions.AsNoTracking(),
                registration => registration.EventPositionId,
                position => position.Id,
                (registration, position) => new { registration, position })
            .Join(
                dbContext.Events.AsNoTracking(),
                candidate => candidate.position.EventId,
                @event => @event.Id,
                (candidate, @event) => new MemberRegistrationHistoryRow(
                    @event.Id,
                    @event.Name,
                    @event.StartsAt,
                    @event.Status,
                    candidate.registration.Id,
                    candidate.position.Id,
                    candidate.position.Name,
                    candidate.registration.Status,
                    candidate.registration.RequestedAt,
                    candidate.registration.DecidedAt,
                    candidate.registration.AssignedAt,
                    candidate.registration.CancelledAt))
            .ToListAsync(cancellationToken);

        var attendances = await dbContext.EventAttendances
            .AsNoTracking()
            .Where(attendance => attendance.MemberId == memberId)
            .Join(
                dbContext.Events.AsNoTracking(),
                attendance => attendance.EventId,
                @event => @event.Id,
                (attendance, @event) => new MemberAttendanceHistoryRow(
                    @event.Id,
                    @event.Name,
                    @event.StartsAt,
                    @event.Status,
                    new EventAttendanceView(
                        attendance.Id,
                        attendance.EventId,
                        attendance.MemberId,
                        attendance.CheckedInAt,
                        attendance.CheckedInByMemberId,
                        attendance.CreatedAt)))
            .ToListAsync(cancellationToken);

        var history = new Dictionary<Guid, MemberHistoryBuilder>();
        foreach (var registration in registrations)
        {
            var item = GetOrCreateHistoryItem(history, registration.EventId, registration.EventName, registration.StartsAt, registration.EventStatus);
            item.Registrations.Add(new MemberEventRegistrationHistoryView(
                registration.RegistrationId,
                registration.EventPositionId,
                registration.EventPositionName,
                registration.RegistrationStatus,
                registration.RequestedAt,
                registration.DecidedAt,
                registration.AssignedAt,
                registration.CancelledAt));
        }

        foreach (var attendance in attendances)
        {
            var item = GetOrCreateHistoryItem(history, attendance.EventId, attendance.EventName, attendance.StartsAt, attendance.EventStatus);
            item.Attendance = attendance.Attendance;
        }

        return history.Values
            .OrderByDescending(item => item.StartsAt)
            .Select(item => new MemberEventHistoryView(
                item.EventId,
                item.EventName,
                item.StartsAt,
                item.EventStatus,
                item.Registrations.OrderBy(registration => registration.EventPositionName).ToArray(),
                item.Attendance))
            .ToArray();
    }

    public Task AddEventAsync(Event @event, AuditLog auditLog, CancellationToken cancellationToken)
    {
        dbContext.Events.Add(@event);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task UpdateEventAsync(Event @event, AuditLog auditLog, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task AddPositionAsync(EventPosition position, AuditLog auditLog, CancellationToken cancellationToken)
    {
        dbContext.EventPositions.Add(position);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task UpdatePositionAsync(
        EventPosition position,
        Event eventItem,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task SaveRegistrationAsync(
        EventRegistration registration,
        bool isNew,
        Event eventItem,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        if (isNew)
        {
            dbContext.EventRegistrations.Add(registration);
        }

        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    public Task AddAttendanceAsync(
        EventAttendance attendance,
        Event eventItem,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.EventAttendances.Add(attendance);
        dbContext.AuditLogs.Add(auditLog);
        return SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<EventView>> CreateViewsAsync(
        List<Event> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return [];
        }

        var eventIds = events.Select(@event => @event.Id).ToArray();
        var positions = await dbContext.EventPositions
            .AsNoTracking()
            .Where(position => eventIds.Contains(position.EventId))
            .OrderBy(position => position.SortOrder)
            .ThenBy(position => position.Name)
            .ToListAsync(cancellationToken);

        return events.Select(@event => new EventView(
            @event.Id,
            @event.Name,
            @event.Description,
            @event.Location,
            @event.StartsAt,
            @event.EndsAt,
            @event.Status,
            @event.AllowMultiplePositions,
            @event.CreatedByMemberId,
            positions
                .Where(position => position.EventId == @event.Id)
                .Select(position => new EventPositionView(
                    position.Id,
                    position.EventId,
                    position.Name,
                    position.Description,
                    position.Capacity,
                    position.RequiredDepartmentId,
                    position.SortOrder,
                    position.CreatedAt,
                    position.UpdatedAt))
                .ToArray(),
            @event.CreatedAt,
            @event.UpdatedAt)).ToArray();
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new EventConflictException("The event or its position was changed by another request.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23503" })
        {
            throw new EventValidationException("The referenced member or department no longer exists.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new EventConflictException("The event record conflicts with an existing record.");
        }
    }

    private static MemberHistoryBuilder GetOrCreateHistoryItem(
        IDictionary<Guid, MemberHistoryBuilder> history,
        Guid eventId,
        string eventName,
        DateTimeOffset startsAt,
        EventStatus eventStatus)
    {
        if (history.TryGetValue(eventId, out var item))
        {
            return item;
        }

        item = new MemberHistoryBuilder(eventId, eventName, startsAt, eventStatus);
        history.Add(eventId, item);
        return item;
    }

    private sealed class MemberHistoryBuilder(Guid eventId, string eventName, DateTimeOffset startsAt, EventStatus eventStatus)
    {
        public Guid EventId { get; } = eventId;

        public string EventName { get; } = eventName;

        public DateTimeOffset StartsAt { get; } = startsAt;

        public EventStatus EventStatus { get; } = eventStatus;

        public List<MemberEventRegistrationHistoryView> Registrations { get; } = [];

        public EventAttendanceView? Attendance { get; set; }
    }

    private sealed record MemberRegistrationHistoryRow(
        Guid EventId,
        string EventName,
        DateTimeOffset StartsAt,
        EventStatus EventStatus,
        Guid RegistrationId,
        Guid EventPositionId,
        string EventPositionName,
        EventRegistrationStatus RegistrationStatus,
        DateTimeOffset? RequestedAt,
        DateTimeOffset? DecidedAt,
        DateTimeOffset? AssignedAt,
        DateTimeOffset? CancelledAt);

    private sealed record MemberAttendanceHistoryRow(
        Guid EventId,
        string EventName,
        DateTimeOffset StartsAt,
        EventStatus EventStatus,
        EventAttendanceView Attendance);
}
