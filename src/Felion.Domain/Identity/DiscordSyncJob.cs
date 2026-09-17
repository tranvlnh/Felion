using Felion.Domain.Common;

namespace Felion.Domain.Identity;

public sealed class DiscordSyncJob
{
    private DiscordSyncJob()
    {
    }

    private DiscordSyncJob(
        Guid id,
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        DiscordSyncOperation operation,
        string payloadJson,
        DateTimeOffset now)
    {
        Id = id;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Operation = operation;
        PayloadJson = payloadJson;
        Status = DiscordSyncJobStatus.Pending;
        Attempts = 0;
        CreatedAt = now;
        UpdatedAt = now;
        NextAttemptAt = now;
    }

    public Guid Id { get; private set; }

    public DiscordIdentitySubjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public DiscordSyncOperation Operation { get; private set; }

    public string PayloadJson { get; private set; } = string.Empty;

    public DiscordSyncJobStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DiscordSyncJob Create(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        DiscordSyncOperation operation,
        string payloadJson,
        DateTimeOffset? now = null)
    {
        if (!Enum.IsDefined(subjectType))
        {
            throw new DomainException("Unknown Discord identity subject type.");
        }

        if (subjectId == Guid.Empty)
        {
            throw new DomainException("Discord sync subject is required.");
        }

        if (!Enum.IsDefined(operation))
        {
            throw new DomainException("Unknown Discord sync operation.");
        }

        var normalizedPayload = IdentityNormalizer.RequiredText(payloadJson, nameof(PayloadJson));
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new DiscordSyncJob(
            Guid.NewGuid(),
            subjectType,
            subjectId,
            operation,
            normalizedPayload,
            timestamp);
    }

    public void MarkRunning(DateTimeOffset? now = null)
    {
        if (Status is not (DiscordSyncJobStatus.Pending or DiscordSyncJobStatus.Failed))
        {
            throw new DomainException("Only pending or failed Discord sync jobs can run.");
        }

        Status = DiscordSyncJobStatus.Running;
        Attempts++;
        LastError = null;
        Touch(now);
    }

    public void MarkSucceeded(DateTimeOffset? now = null)
    {
        if (Status != DiscordSyncJobStatus.Running)
        {
            throw new DomainException("Only running Discord sync jobs can succeed.");
        }

        Status = DiscordSyncJobStatus.Succeeded;
        Touch(now);
    }

    public void MarkFailed(string error, DateTimeOffset? now = null)
    {
        var normalizedError = IdentityNormalizer.RequiredText(error, nameof(LastError));
        Status = DiscordSyncJobStatus.Failed;
        LastError = normalizedError;
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        NextAttemptAt = timestamp.Add(GetRetryDelay(Attempts));
        Touch(timestamp);
    }

    private static TimeSpan GetRetryDelay(int attempts)
    {
        return attempts switch
        {
            <= 1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromSeconds(30),
            3 => TimeSpan.FromMinutes(5),
            4 => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromHours(1)
        };
    }

    private void Touch(DateTimeOffset? now)
    {
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
