using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public interface IDiscordSyncJobStore
{
    public Task<DiscordSyncJob?> ClaimNextAsync(CancellationToken cancellationToken);

    public Task SaveJobAsync(
        DiscordSyncJob job,
        CancellationToken cancellationToken);
}

public interface IDiscordSyncProcessor
{
    public Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}
