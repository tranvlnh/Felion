using System.Text.Json;
using Felion.Application.Hardening;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public sealed class DiscordLinkingService(
    IDiscordLinkStore store,
    IRateLimitGate rateLimitGate) : IDiscordLinkingService
{
    private const int MaxStudentIdLength = 50;

    public async Task<DiscordLinkResult> LinkAsync(
        long discordUserId,
        string studentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (discordUserId <= 0)
        {
            throw new DiscordLinkValidationException("Discord user ID must be a positive signed snowflake.");
        }

        var rateLimit = await rateLimitGate.TryAcquireAsync(
            RateLimitOperation.DiscordLink,
            discordUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cancellationToken);
        if (!rateLimit.IsAcquired)
        {
            throw new RateLimitExceededException(RateLimitOperation.DiscordLink, rateLimit.RetryAfter);
        }

        string normalizedStudentId;
        try
        {
            normalizedStudentId = IdentityNormalizer.StudentId(studentId);
        }
        catch (DomainException exception)
        {
            throw new DiscordLinkValidationException(exception.Message);
        }

        if (normalizedStudentId.Length > MaxStudentIdLength)
        {
            throw new DiscordLinkValidationException(
                $"StudentId must be at most {MaxStudentIdLength} characters.");
        }

        var subjects = await store.FindEligibleSubjectsByStudentIdAsync(
            normalizedStudentId,
            cancellationToken);
        if (subjects.Count == 0)
        {
            throw new DiscordLinkSubjectNotFoundException(normalizedStudentId);
        }

        if (subjects.Count > 1)
        {
            throw new DiscordLinkSubjectAmbiguousException(normalizedStudentId);
        }

        var subject = subjects[0];
        var existingByStudentId = await store.FindByStudentIdAsync(
            normalizedStudentId,
            cancellationToken);
        var existingByDiscordUserId = await store.FindByDiscordUserIdAsync(
            discordUserId,
            cancellationToken);

        if (existingByStudentId is not null)
        {
            if (existingByStudentId.DiscordUserId != discordUserId)
            {
                throw new DiscordLinkConflictException(
                    "This StudentId is already linked to another Discord user.");
            }

            if (existingByStudentId.SubjectId != subject.SubjectId
                || existingByStudentId.SubjectType != subject.SubjectType)
            {
                throw new DiscordLinkConflictException(
                    "This StudentId is linked to a different identity.");
            }

            return ToResult(subject, existingByStudentId, syncQueued: false);
        }

        if (existingByDiscordUserId is not null)
        {
            throw new DiscordLinkConflictException(
                "This Discord user is already linked to another identity.");
        }

        DiscordIdentityLink link;
        try
        {
            link = DiscordIdentityLink.Create(
                discordUserId,
                normalizedStudentId,
                subject.SubjectType,
                subject.SubjectId);
        }
        catch (DomainException exception)
        {
            throw new DiscordLinkValidationException(exception.Message);
        }

        var audit = AuditLog.Create(
            AuditActorType.DiscordMember,
            actorMemberId: null,
            actorDiscordUserId: discordUserId,
            action: "DiscordIdentityLinked",
            entityType: subject.SubjectType == DiscordIdentitySubjectType.Member
                ? "Member"
                : "ProbationCandidate",
            entityId: subject.SubjectId,
            correlationId: correlationId,
            afterJson: JsonSerializer.Serialize(new
            {
                link.Id,
                link.DiscordUserId,
                link.StudentId,
                link.SubjectType,
                link.SubjectId,
                link.LinkedAt
            }));
        var syncJob = DiscordSyncJob.Create(
            subject.SubjectType,
            subject.SubjectId,
            DiscordSyncOperation.SynchronizeRoles,
            JsonSerializer.Serialize(new
            {
                link.DiscordUserId,
                link.StudentId,
                link.SubjectType,
                link.SubjectId
            }));

        try
        {
            await store.AddAsync(link, audit, syncJob, cancellationToken);
        }
        catch (DiscordLinkConflictException)
        {
            throw;
        }

        return ToResult(subject, link, syncQueued: true);
    }

    private static DiscordLinkResult ToResult(
        DiscordLinkSubject subject,
        DiscordIdentityLink link,
        bool syncQueued)
    {
        return new DiscordLinkResult(
            subject.SubjectId,
            subject.SubjectType,
            subject.StudentId,
            subject.DisplayName,
            link.DiscordUserId,
            link.LinkedAt,
            syncQueued);
    }
}
