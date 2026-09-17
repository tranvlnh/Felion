using System.Text.Json;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed class DiscordVerificationMessageService(
    IDiscordVerificationMessageStore store,
    IMemberStore memberStore,
    IDiscordVerificationMessageGateway? gateway = null,
    IDiscordAuthorizationService? authorizationService = null,
    IDiscordGuildPermissionGateway? guildPermissionGateway = null) : IDiscordVerificationMessageService
{
    public async Task<DiscordVerificationMessageSnapshot> PublishAsync(
        Guid actorMemberId,
        long channelId,
        string correlationId,
        CancellationToken cancellationToken,
        long? actorDiscordUserId = null)
    {
        await EnsureAdminAsync(actorMemberId, cancellationToken);

        return await PublishAndAuditAsync(
            actorMemberId,
            channelId,
            correlationId,
            actorDiscordUserId,
            actorDiscordUserId is null ? "WebAdmin" : "LinkedDiscordAdmin",
            cancellationToken);
    }

    public async Task<DiscordVerificationMessageSnapshot> PublishFromDiscordAsync(
        long actorDiscordUserId,
        long channelId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (actorDiscordUserId <= 0)
        {
            throw new DiscordVerificationMessageAccessDeniedException();
        }

        var linkedAdmin = authorizationService is not null
            ? await authorizationService.FindAdminAsync(actorDiscordUserId, cancellationToken)
            : null;
        var authorizationMode = linkedAdmin is not null
            ? "LinkedDiscordAdmin"
            : await IsServerAdministratorAsync(actorDiscordUserId, cancellationToken)
                ? "DiscordServerAdministrator"
                : throw new DiscordVerificationMessageAccessDeniedException();

        return await PublishAndAuditAsync(
            null,
            channelId,
            correlationId,
            actorDiscordUserId,
            authorizationMode,
            cancellationToken);
    }

    private async Task<DiscordVerificationMessageSnapshot> PublishAndAuditAsync(
        Guid? actorMemberId,
        long channelId,
        string correlationId,
        long? actorDiscordUserId,
        string authorizationMode,
        CancellationToken cancellationToken)
    {

        if (channelId <= 0)
        {
            throw new DiscordVerificationMessageValidationException(
                "Discord channel ID must be a positive signed snowflake.");
        }

        if (gateway is null)
        {
            throw new DiscordVerificationMessageUnavailableException();
        }

        var message = await gateway.PublishAsync(channelId, cancellationToken);
        var audit = AuditLog.Create(
            actorDiscordUserId is null ? AuditActorType.WebMember : AuditActorType.DiscordMember,
            actorDiscordUserId is null ? actorMemberId : null,
            actorDiscordUserId,
            "DiscordVerificationMessagePublished",
            "DiscordVerificationMessage",
            entityId: null,
            correlationId,
            afterJson: JsonSerializer.Serialize(new
            {
                message.ChannelId,
                message.MessageId,
                AuthorizationMode = authorizationMode
            }));
        await store.AddAuditAsync(audit, cancellationToken);

        return message;
    }

    private async Task<bool> IsServerAdministratorAsync(
        long actorDiscordUserId,
        CancellationToken cancellationToken)
    {
        if (guildPermissionGateway is null)
        {
            throw new DiscordVerificationMessageUnavailableException();
        }

        return await guildPermissionGateway.IsAdministratorAsync(
            actorDiscordUserId,
            cancellationToken);
    }

    private async Task EnsureAdminAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position != MemberPosition.Admin)
        {
            throw new DiscordVerificationMessageAccessDeniedException();
        }
    }
}
