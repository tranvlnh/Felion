using System.Diagnostics.CodeAnalysis;
using Felion.Domain.Common;

namespace Felion.Domain.Events;

[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Event is the approved aggregate name in the Felion data model.")]
public sealed class Event
{
    private Event()
    {
    }

    private Event(
        Guid id,
        string name,
        string? description,
        string? location,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt,
        bool allowMultiplePositions,
        Guid createdByMemberId,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Description = description;
        Location = location;
        StartsAt = startsAt;
        EndsAt = endsAt;
        AllowMultiplePositions = allowMultiplePositions;
        CreatedByMemberId = createdByMemberId;
        Status = EventStatus.Draft;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Location { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public EventStatus Status { get; private set; }

    public bool AllowMultiplePositions { get; private set; }

    public Guid CreatedByMemberId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Event Create(
        string name,
        string? description,
        string? location,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt,
        bool allowMultiplePositions,
        Guid createdByMemberId,
        DateTimeOffset? now = null)
    {
        if (createdByMemberId == Guid.Empty)
        {
            throw new DomainException("Event creator is required.");
        }

        var normalizedStartsAt = startsAt.ToUniversalTime();
        var normalizedEndsAt = endsAt?.ToUniversalTime();
        ValidateSchedule(normalizedStartsAt, normalizedEndsAt);

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new Event(
            Guid.NewGuid(),
            NormalizeName(name),
            NormalizeOptionalText(description, nameof(Description)),
            NormalizeOptionalText(location, nameof(Location)),
            normalizedStartsAt,
            normalizedEndsAt,
            allowMultiplePositions,
            createdByMemberId,
            timestamp);
    }

    public void Update(
        string? name,
        string? description,
        string? location,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        bool? allowMultiplePositions,
        DateTimeOffset? now = null)
    {
        EnsureMutable();

        var updatedStartsAt = startsAt?.ToUniversalTime() ?? StartsAt;
        var updatedEndsAt = endsAt?.ToUniversalTime() ?? EndsAt;
        ValidateSchedule(updatedStartsAt, updatedEndsAt);

        if (name is not null)
        {
            Name = NormalizeName(name);
        }

        if (description is not null)
        {
            Description = NormalizeOptionalText(description, nameof(Description));
        }

        if (location is not null)
        {
            Location = NormalizeOptionalText(location, nameof(Location));
        }

        StartsAt = updatedStartsAt;
        EndsAt = updatedEndsAt;
        if (allowMultiplePositions is not null)
        {
            AllowMultiplePositions = allowMultiplePositions.Value;
        }

        Touch(now);
    }

    public void Publish(bool hasPositions, DateTimeOffset? now = null)
    {
        if (Status != EventStatus.Draft)
        {
            throw new DomainException("Only a draft event can be published.");
        }

        if (!hasPositions)
        {
            throw new DomainException("An event must have at least one position before publishing.");
        }

        Status = EventStatus.Published;
        Touch(now);
    }

    public void CloseRegistration(DateTimeOffset? now = null)
    {
        Transition(EventStatus.Published, EventStatus.RegistrationClosed, now);
    }

    public void Start(DateTimeOffset? now = null)
    {
        Transition(EventStatus.RegistrationClosed, EventStatus.InProgress, now);
    }

    public void Complete(DateTimeOffset? now = null)
    {
        Transition(EventStatus.InProgress, EventStatus.Completed, now);
    }

    public void Cancel(DateTimeOffset? now = null)
    {
        if (Status is EventStatus.Completed or EventStatus.Cancelled)
        {
            throw new DomainException("A completed or cancelled event cannot be cancelled.");
        }

        Status = EventStatus.Cancelled;
        Touch(now);
    }

    public void RecordRegistrationChange(DateTimeOffset? now = null)
    {
        Touch(now);
    }

    public void RecordAttendanceChange(DateTimeOffset? now = null)
    {
        Touch(now);
    }

    private void Transition(EventStatus expected, EventStatus next, DateTimeOffset? now)
    {
        if (Status != expected)
        {
            throw new DomainException($"An event can transition to {next} only from {expected}.");
        }

        Status = next;
        Touch(now);
    }

    private void EnsureMutable()
    {
        if (Status is EventStatus.Completed or EventStatus.Cancelled)
        {
            throw new DomainException("A completed or cancelled event cannot be edited.");
        }
    }

    private static string NormalizeName(string name)
    {
        var normalized = IdentityNormalizer.RequiredText(name, nameof(Name));
        if (normalized.Length > 200)
        {
            throw new DomainException("Event name must be at most 200 characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value) ? null : IdentityNormalizer.RequiredText(value, field);
    }

    private static void ValidateSchedule(DateTimeOffset startsAt, DateTimeOffset? endsAt)
    {
        if (endsAt is not null && endsAt <= startsAt)
        {
            throw new DomainException("Event end must be later than its start.");
        }
    }

    private void Touch(DateTimeOffset? now)
    {
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
