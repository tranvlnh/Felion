namespace Felion.Application.Discord;

public sealed record DiscordAdminActor(Guid MemberId, long DiscordUserId);

public interface IDiscordAuthorizationService
{
    public Task<DiscordAdminActor?> FindAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken);

    public Task<DiscordAdminActor> RequireAdminAsync(
        long discordUserId,
        CancellationToken cancellationToken);
}

public interface IDiscordGuildPermissionGateway
{
    public Task<bool> IsAdministratorAsync(
        long discordUserId,
        CancellationToken cancellationToken);
}
