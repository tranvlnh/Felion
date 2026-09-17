using Felion.Application.Discord;
using Felion.Domain.Identity;
using Felion.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Felion.IntegrationTests;

public sealed class DiscordSyncJobStoreTests
{
    [PostgreSqlFact]
    public async Task ClaimNextAsyncSkipsFailedJobsUntilTheirNextAttempt()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var readyJob = DiscordSyncJob.Create(
            DiscordIdentitySubjectType.Member,
            Guid.NewGuid(),
            DiscordSyncOperation.KickUser,
            "{}",
            now.AddMinutes(-1));
        var delayedJob = DiscordSyncJob.Create(
            DiscordIdentitySubjectType.Member,
            Guid.NewGuid(),
            DiscordSyncOperation.KickUser,
            "{}",
            now);
        delayedJob.MarkRunning(now);
        delayedJob.MarkFailed("Discord is temporarily unavailable.", now);

        await using (var context = database.CreateContext())
        {
            context.DiscordSyncJobs.AddRange(readyJob, delayedJob);
            await context.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = database.ConnectionString
            })
            .Build();
        services.AddFelionInfrastructure(configuration, services.AddHealthChecks());

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDiscordSyncJobStore>();

        var claimed = await store.ClaimNextAsync(CancellationToken.None);

        Assert.NotNull(claimed);
        Assert.Equal(readyJob.Id, claimed.Id);
        Assert.Equal(DiscordSyncJobStatus.Running, claimed.Status);
        Assert.Equal(1, claimed.Attempts);
    }
}
