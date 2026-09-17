using Felion.Domain.Common;

namespace Felion.Domain.Events;

public sealed class EventPosition
{
    private EventPosition()
    {
    }

    private EventPosition(
        Guid id,
        Guid eventId,
        string name,
        string? description,
        int capacity,
        Guid? requiredDepartmentId,
        int sortOrder,
        DateTimeOffset now)
    {
        Id = id;
        EventId = eventId;
        Name = name;
        Description = description;
        Capacity = capacity;
        RequiredDepartmentId = requiredDepartmentId;
        SortOrder = sortOrder;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int Capacity { get; private set; }

    public Guid? RequiredDepartmentId { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static EventPosition Create(
        Guid eventId,
        string name,
        string? description,
        int capacity,
        Guid? requiredDepartmentId,
        int sortOrder,
        DateTimeOffset? now = null)
    {
        if (eventId == Guid.Empty)
        {
            throw new DomainException("Event is required.");
        }

        ValidateCapacity(capacity);
        ValidateSortOrder(sortOrder);
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EventPosition(
            Guid.NewGuid(),
            eventId,
            NormalizeName(name),
            NormalizeOptionalText(description),
            capacity,
            NormalizeDepartmentId(requiredDepartmentId),
            sortOrder,
            timestamp);
    }

    public void Update(
        string? name,
        string? description,
        int? capacity,
        Guid? requiredDepartmentId,
        int? sortOrder,
        DateTimeOffset? now = null)
    {
        if (name is not null)
        {
            Name = NormalizeName(name);
        }

        if (description is not null)
        {
            Description = NormalizeOptionalText(description);
        }

        if (capacity is not null)
        {
            ValidateCapacity(capacity.Value);
            Capacity = capacity.Value;
        }

        if (requiredDepartmentId is not null)
        {
            RequiredDepartmentId = NormalizeDepartmentId(requiredDepartmentId);
        }

        if (sortOrder is not null)
        {
            ValidateSortOrder(sortOrder.Value);
            SortOrder = sortOrder.Value;
        }

        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    private static string NormalizeName(string name)
    {
        var normalized = IdentityNormalizer.RequiredText(name, nameof(Name));
        if (normalized.Length > 200)
        {
            throw new DomainException("Event position name must be at most 200 characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : IdentityNormalizer.RequiredText(value, nameof(Description));
    }

    private static Guid? NormalizeDepartmentId(Guid? departmentId)
    {
        if (departmentId == Guid.Empty)
        {
            throw new DomainException("Required department must be a valid identifier.");
        }

        return departmentId;
    }

    private static void ValidateCapacity(int capacity)
    {
        if (capacity <= 0)
        {
            throw new DomainException("Event position capacity must be greater than zero.");
        }
    }

    private static void ValidateSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new DomainException("Event position sort order cannot be negative.");
        }
    }
}
