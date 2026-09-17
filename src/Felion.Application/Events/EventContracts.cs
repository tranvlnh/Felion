using Felion.Domain.Audit;
using Felion.Domain.Events;

namespace Felion.Application.Events;

public sealed record CreateEventCommand(
    string Name,
    string? Description,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool AllowMultiplePositions = false);

public sealed record UpdateEventCommand(
    string? Name = null,
    string? Description = null,
    string? Location = null,
    DateTimeOffset? StartsAt = null,
    DateTimeOffset? EndsAt = null,
    bool? AllowMultiplePositions = null);

public sealed record CreateEventPositionCommand(
    string Name,
    string? Description,
    int Capacity,
    Guid? RequiredDepartmentId,
    int SortOrder);

public sealed record UpdateEventPositionCommand(
    string? Name = null,
    string? Description = null,
    int? Capacity = null,
    Guid? RequiredDepartmentId = null,
    int? SortOrder = null);

public sealed record EventRegistrationDto(
    Guid Id,
    Guid EventPositionId,
    Guid MemberId,
    EventRegistrationStatus Status,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? DecidedAt,
    Guid? DecidedByMemberId,
    DateTimeOffset? AssignedAt,
    Guid? AssignedByMemberId,
    DateTimeOffset? CancelledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EventRegistrationView(
    Guid Id,
    Guid EventId,
    Guid EventPositionId,
    Guid MemberId,
    EventRegistrationStatus Status,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? DecidedAt,
    Guid? DecidedByMemberId,
    DateTimeOffset? AssignedAt,
    Guid? AssignedByMemberId,
    DateTimeOffset? CancelledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EventAttendanceDto(
    Guid Id,
    Guid EventId,
    Guid MemberId,
    DateTimeOffset CheckedInAt,
    Guid CheckedInByMemberId,
    DateTimeOffset CreatedAt);

public sealed record EventAttendanceView(
    Guid Id,
    Guid EventId,
    Guid MemberId,
    DateTimeOffset CheckedInAt,
    Guid CheckedInByMemberId,
    DateTimeOffset CreatedAt);

public sealed record MemberEventRegistrationHistoryView(
    Guid Id,
    Guid EventPositionId,
    string EventPositionName,
    EventRegistrationStatus Status,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? CancelledAt);

public sealed record MemberEventHistoryView(
    Guid EventId,
    string EventName,
    DateTimeOffset StartsAt,
    EventStatus EventStatus,
    IReadOnlyList<MemberEventRegistrationHistoryView> Registrations,
    EventAttendanceView? Attendance);

public sealed record MemberEventRegistrationHistoryDto(
    Guid Id,
    Guid EventPositionId,
    string EventPositionName,
    EventRegistrationStatus Status,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? CancelledAt);

public sealed record MemberEventHistoryDto(
    Guid EventId,
    string EventName,
    DateTimeOffset StartsAt,
    EventStatus EventStatus,
    IReadOnlyList<MemberEventRegistrationHistoryDto> Registrations,
    EventAttendanceDto? Attendance);

public sealed record EventPositionView(
    Guid Id,
    Guid EventId,
    string Name,
    string? Description,
    int Capacity,
    Guid? RequiredDepartmentId,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EventView(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    EventStatus Status,
    bool AllowMultiplePositions,
    Guid CreatedByMemberId,
    IReadOnlyList<EventPositionView> Positions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EventPositionDto(
    Guid Id,
    Guid EventId,
    string Name,
    string? Description,
    int Capacity,
    Guid? RequiredDepartmentId,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EventDto(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    EventStatus Status,
    bool AllowMultiplePositions,
    Guid CreatedByMemberId,
    IReadOnlyList<EventPositionDto> Positions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IEventStore
{
    public Task<IReadOnlyList<EventView>> ListViewsAsync(
        bool includeUnpublished,
        CancellationToken cancellationToken);

    public Task<EventView?> FindViewAsync(Guid eventId, CancellationToken cancellationToken);

    public Task<Event?> FindEventAsync(Guid eventId, bool track, CancellationToken cancellationToken);

    public Task<EventPosition?> FindPositionAsync(
        Guid eventId,
        Guid positionId,
        bool track,
        CancellationToken cancellationToken);

    public Task<EventRegistration?> FindRegistrationAsync(
        Guid eventId,
        Guid registrationId,
        bool track,
        CancellationToken cancellationToken);

    public Task<EventRegistration?> FindActiveRegistrationAsync(
        Guid eventPositionId,
        Guid memberId,
        CancellationToken cancellationToken);

    public Task<int> CountCapacityConsumingRegistrationsAsync(
        Guid eventPositionId,
        CancellationToken cancellationToken);

    public Task<bool> HasActiveRegistrationForEventAsync(
        Guid eventId,
        Guid memberId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<EventRegistrationView>> ListRegistrationViewsAsync(
        Guid eventId,
        CancellationToken cancellationToken);

    public Task<EventAttendance?> FindAttendanceAsync(
        Guid eventId,
        Guid memberId,
        bool track,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<EventAttendanceView>> ListAttendanceViewsAsync(
        Guid eventId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<MemberEventHistoryView>> ListMemberEventHistoryViewsAsync(
        Guid memberId,
        CancellationToken cancellationToken);

    public Task<int> CountPositionsAsync(Guid eventId, CancellationToken cancellationToken);

    public Task AddEventAsync(Event eventItem, AuditLog auditLog, CancellationToken cancellationToken);

    public Task UpdateEventAsync(Event eventItem, AuditLog auditLog, CancellationToken cancellationToken);

    public Task AddPositionAsync(EventPosition position, AuditLog auditLog, CancellationToken cancellationToken);

    public Task UpdatePositionAsync(
        EventPosition position,
        Event eventItem,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task SaveRegistrationAsync(
        EventRegistration registration,
        bool isNew,
        Event eventItem,
        AuditLog auditLog,
        CancellationToken cancellationToken);

    public Task AddAttendanceAsync(
        EventAttendance attendance,
        Event eventItem,
        AuditLog auditLog,
        CancellationToken cancellationToken);
}

public interface IEventManagementService
{
    public Task<IReadOnlyList<EventDto>> ListAsync(Guid actorMemberId, CancellationToken cancellationToken);

    public Task<EventDto> GetAsync(Guid actorMemberId, Guid eventId, CancellationToken cancellationToken);

    public Task<EventDto> CreateAsync(
        Guid actorMemberId,
        CreateEventCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventDto> UpdateAsync(
        Guid actorMemberId,
        Guid eventId,
        UpdateEventCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventDto> PublishAsync(
        Guid actorMemberId,
        Guid eventId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventDto> CloseRegistrationAsync(
        Guid actorMemberId,
        Guid eventId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventDto> StartAsync(
        Guid actorMemberId,
        Guid eventId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventDto> CompleteAsync(
        Guid actorMemberId,
        Guid eventId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventDto> CancelAsync(
        Guid actorMemberId,
        Guid eventId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventPositionDto> AddPositionAsync(
        Guid actorMemberId,
        Guid eventId,
        CreateEventPositionCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventPositionDto> UpdatePositionAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid positionId,
        UpdateEventPositionCommand command,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventRegistrationDto> RegisterAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid positionId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<EventRegistrationDto>> ListRegistrationsAsync(
        Guid actorMemberId,
        Guid eventId,
        CancellationToken cancellationToken);

    public Task<EventRegistrationDto> ApproveRegistrationAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid registrationId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventRegistrationDto> RejectRegistrationAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid registrationId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventRegistrationDto> AssignAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid positionId,
        Guid memberId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<EventAttendanceDto> CheckInAsync(
        Guid actorMemberId,
        Guid eventId,
        Guid memberId,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<EventAttendanceDto>> ListAttendanceAsync(
        Guid actorMemberId,
        Guid eventId,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<MemberEventHistoryDto>> ListMemberHistoryAsync(
        Guid actorMemberId,
        Guid memberId,
        CancellationToken cancellationToken);
}
