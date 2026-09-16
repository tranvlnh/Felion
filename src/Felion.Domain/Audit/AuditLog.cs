using System.Text.Json;
using Felion.Domain.Common;

namespace Felion.Domain.Audit;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    private AuditLog(
        Guid id,
        DateTimeOffset occurredAt,
        AuditActorType actorType,
        Guid? actorMemberId,
        long? actorDiscordUserId,
        string action,
        string entityType,
        Guid? entityId,
        string correlationId,
        string metadataJson,
        string? beforeJson,
        string? afterJson)
    {
        Id = id;
        OccurredAt = occurredAt;
        ActorType = actorType;
        ActorMemberId = actorMemberId;
        ActorDiscordUserId = actorDiscordUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        CorrelationId = correlationId;
        MetadataJson = metadataJson;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public AuditActorType ActorType { get; private set; }

    public Guid? ActorMemberId { get; private set; }

    public long? ActorDiscordUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string MetadataJson { get; private set; } = "{}";

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }

    public static AuditLog Create(
        AuditActorType actorType,
        Guid? actorMemberId,
        long? actorDiscordUserId,
        string action,
        string entityType,
        Guid? entityId,
        string correlationId,
        string? metadataJson = null,
        string? beforeJson = null,
        string? afterJson = null,
        DateTimeOffset? occurredAt = null)
    {
        ValidateActor(actorType, actorMemberId, actorDiscordUserId);

        if (actorDiscordUserId is <= 0)
        {
            throw new DomainException("Discord actor ID must be positive.");
        }

        var normalizedAction = IdentityNormalizer.RequiredText(action, nameof(Action));
        var normalizedEntityType = IdentityNormalizer.RequiredText(entityType, nameof(EntityType));
        var normalizedCorrelationId = IdentityNormalizer.RequiredText(correlationId, nameof(CorrelationId));
        var normalizedMetadata = NormalizeJson(metadataJson, "MetadataJson") ?? "{}";
        var normalizedBefore = NormalizeJson(beforeJson, "BeforeJson");
        var normalizedAfter = NormalizeJson(afterJson, "AfterJson");

        return new AuditLog(
            Guid.NewGuid(),
            (occurredAt ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            actorType,
            actorMemberId,
            actorDiscordUserId,
            normalizedAction,
            normalizedEntityType,
            entityId,
            normalizedCorrelationId,
            normalizedMetadata,
            normalizedBefore,
            normalizedAfter);
    }

    private static void ValidateActor(
        AuditActorType actorType,
        Guid? actorMemberId,
        long? actorDiscordUserId)
    {
        switch (actorType)
        {
            case AuditActorType.WebMember when actorMemberId is not null && actorDiscordUserId is null:
            case AuditActorType.DiscordMember when actorMemberId is null && actorDiscordUserId is not null:
            case AuditActorType.System when actorMemberId is null && actorDiscordUserId is null:
                return;
            case AuditActorType.WebMember:
                throw new DomainException("A web member actor requires only ActorMemberId.");
            case AuditActorType.DiscordMember:
                throw new DomainException("A Discord member actor requires only ActorDiscordUserId.");
            case AuditActorType.System:
                throw new DomainException("A system actor cannot have a member identity.");
            default:
                throw new DomainException("Unknown audit actor type.");
        }
    }

    private static string? NormalizeJson(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value is null ? null : "{}";
        }

        var normalized = value.Trim();
        try
        {
            using var document = JsonDocument.Parse(normalized);
        }
        catch (JsonException exception)
        {
            throw new DomainException($"{fieldName} must contain valid JSON: {exception.Message}");
        }

        return normalized;
    }
}
