using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public interface IDiscordSyncJobStore
{
    public Task<DiscordSyncJob?> ClaimNextAsync(CancellationToken cancellationToken);

    public Task<DiscordRoleSyncTarget?> FindTargetAsync(
        DiscordSyncJob job,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<DiscordRoleMapping>> ListRoleMappingsAsync(
        CancellationToken cancellationToken);

    public Task SaveJobAsync(
        DiscordSyncJob job,
        CancellationToken cancellationToken);
}

public interface IDiscordSyncProcessor
{
    public Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}
