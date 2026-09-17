using Felion.Domain.Audit;

namespace Felion.Application.Discord;

public sealed record DiscordVerificationMessageSnapshot(long ChannelId, long MessageId);

public interface IDiscordVerificationMessageGateway
{
    public Task<DiscordVerificationMessageSnapshot> PublishAsync(
        long channelId,
        CancellationToken cancellationToken);
}

public interface IDiscordVerificationMessageStore
{
    public Task AddAuditAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken);
}

public interface IDiscordVerificationMessageService
{
    public Task<DiscordVerificationMessageSnapshot> PublishAsync(
        Guid actorMemberId,
        long channelId,
        string correlationId,
        CancellationToken cancellationToken,
        long? actorDiscordUserId = null);

    public Task<DiscordVerificationMessageSnapshot> PublishFromDiscordAsync(
        long actorDiscordUserId,
        long channelId,
        string correlationId,
        CancellationToken cancellationToken);
}
