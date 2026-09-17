using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class DiscordSyncProcessorTests
{
    [Fact]
    public async Task ProcessorKicksUserForKickJob()
    {
        var store = new FakeSyncJobStore
        {
            Job = DiscordSyncJob.Create(
                DiscordIdentitySubjectType.Probation,
                Guid.NewGuid(),
                DiscordSyncOperation.KickUser,
                "{\"DiscordUserId\":123456789}")
        };
        var guildGateway = new FakeGuildGateway();
        var processor = new DiscordSyncProcessor(store, guildGateway);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
        Assert.Equal(DiscordSyncJobStatus.Succeeded, store.Job.Status);
        Assert.Equal(123456789, guildGateway.KickedUserId);
    }

    private sealed class FakeSyncJobStore : IDiscordSyncJobStore
    {
        public DiscordSyncJob Job { get; init; } = DiscordSyncJob.Create(
            DiscordIdentitySubjectType.Member,
            Guid.NewGuid(),
            DiscordSyncOperation.KickUser,
            "{}");

        public Task<DiscordSyncJob?> ClaimNextAsync(CancellationToken cancellationToken)
        {
            if (Job.Status == DiscordSyncJobStatus.Pending)
            {
                Job.MarkRunning();
                return Task.FromResult<DiscordSyncJob?>(Job);
            }

            return Task.FromResult<DiscordSyncJob?>(null);
        }

        public Task SaveJobAsync(DiscordSyncJob job, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGuildGateway : IDiscordGuildGateway
    {
        public long KickedUserId { get; private set; }

        public Task KickUserAsync(long discordUserId, CancellationToken cancellationToken)
        {
            KickedUserId = discordUserId;
            return Task.CompletedTask;
        }
    }
}
