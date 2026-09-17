using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed record DiscordAdminActor(Guid MemberId, long DiscordUserId);

public sealed record DiscordManagementActor(
    Guid MemberId,
    long DiscordUserId,
    MemberPosition Position);

public interface IDiscordAuthorizationService
{
    public Task<DiscordAdminActor?> FindAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken);

    public Task<DiscordAdminActor> RequireAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken);

    public Task<DiscordManagementActor?> FindCoreOrAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken);

    public Task<DiscordManagementActor> RequireCoreOrAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken);
}

public interface IDiscordGuildPermissionGateway
{
    public Task<bool> IsAdministratorAsync(
        long discordUserId,
        CancellationToken cancellationToken);
}
