using Felion.Domain.Audit;
using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public sealed record DiscordLinkSubject(
    Guid SubjectId,
    DiscordIdentitySubjectType SubjectType,
    string StudentId,
    string DisplayName);

public sealed record DiscordLinkResult(
    Guid SubjectId,
    DiscordIdentitySubjectType SubjectType,
    string StudentId,
    string DisplayName,
    long DiscordUserId,
    DateTimeOffset LinkedAt,
    bool SyncQueued);

public interface IDiscordLinkStore
{
    public Task<IReadOnlyList<DiscordLinkSubject>> FindEligibleSubjectsByStudentIdAsync(
        string normalizedStudentId,
        CancellationToken cancellationToken);

    public Task<DiscordIdentityLink?> FindByStudentIdAsync(
        string normalizedStudentId,
        CancellationToken cancellationToken);

    public Task<DiscordIdentityLink?> FindByDiscordUserIdAsync(
        long discordUserId,
        CancellationToken cancellationToken);

    public Task AddAsync(
        DiscordIdentityLink link,
        AuditLog auditLog,
        DiscordSyncJob syncJob,
        CancellationToken cancellationToken);
}

public interface IDiscordLinkingService
{
    public Task<DiscordLinkResult> LinkAsync(
        long discordUserId,
        string studentId,
        string correlationId,
        CancellationToken cancellationToken);
}
