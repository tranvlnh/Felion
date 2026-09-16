using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class DiscordSyncProcessorTests
{
    [Fact]
    public async Task ProcessorCalculatesDesiredManagedRolesForAnActiveMember()
    {
        var departmentId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var store = new FakeSyncJobStore
        {
            Target = new DiscordRoleSyncTarget(
                123456789,
                DiscordIdentitySubjectType.Member,
                MemberPosition.Member,
                departmentId,
                generationId,
                ProbationTeamId: null)
        };
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Position,
            "Member",
            10,
            "Member"));
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Position,
            "Core",
            11,
            "Core"));
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Probation,
            "Probation",
            12,
            "Probation"));
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Department,
            departmentId.ToString("D"),
            13,
            "Department"));
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Generation,
            generationId.ToString("D"),
            14,
            "Generation"));
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.ProbationTeam,
            Guid.NewGuid().ToString("D"),
            15,
            "Other team"));

        var gateway = new FakeRoleGateway();
        var processor = new DiscordSyncProcessor(store, gateway);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
        Assert.Equal(DiscordSyncJobStatus.Succeeded, store.Job.Status);
        Assert.Equal([10L, 13L, 14L], gateway.DesiredRoleIds.OrderBy(id => id));
        Assert.Equal([10L, 11L, 12L, 13L, 14L, 15L], gateway.ManagedRoleIds.OrderBy(id => id));
    }

    [Fact]
    public async Task ProcessorMarksJobFailedWithSafeGatewayError()
    {
        var store = new FakeSyncJobStore
        {
            Target = new DiscordRoleSyncTarget(
                123456789,
                DiscordIdentitySubjectType.Member,
                MemberPosition.Member,
                Guid.NewGuid(),
                Guid.NewGuid(),
                ProbationTeamId: null)
        };
        var gateway = new FakeRoleGateway
        {
            Failure = new DiscordRoleGatewayException("Discord rejected the role mutation request.")
        };
        var processor = new DiscordSyncProcessor(store, gateway);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
        Assert.Equal(DiscordSyncJobStatus.Failed, store.Job.Status);
        Assert.Equal("Discord rejected the role mutation request.", store.Job.LastError);
    }

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
        var processor = new DiscordSyncProcessor(store, new FakeRoleGateway(), guildGateway);

        Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
        Assert.Equal(DiscordSyncJobStatus.Succeeded, store.Job.Status);
        Assert.Equal(123456789, guildGateway.KickedUserId);
    }

    private sealed class FakeSyncJobStore : IDiscordSyncJobStore
    {
        public DiscordSyncJob Job { get; init; } = DiscordSyncJob.Create(
            DiscordIdentitySubjectType.Member,
            Guid.NewGuid(),
            DiscordSyncOperation.SynchronizeRoles,
            "{}");

        public DiscordRoleSyncTarget? Target { get; init; }

        public List<DiscordRoleMapping> Mappings { get; } = [];

        public Task<DiscordSyncJob?> ClaimNextAsync(CancellationToken cancellationToken)
        {
            if (Job.Status == DiscordSyncJobStatus.Pending)
            {
                Job.MarkRunning();
                return Task.FromResult<DiscordSyncJob?>(Job);
            }

            return Task.FromResult<DiscordSyncJob?>(null);
        }

        public Task<DiscordRoleSyncTarget?> FindTargetAsync(
            DiscordSyncJob job,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Target);
        }

        public Task<IReadOnlyList<DiscordRoleMapping>> ListRoleMappingsAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<DiscordRoleMapping>>(Mappings);
        }

        public Task SaveJobAsync(DiscordSyncJob job, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoleGateway : IDiscordRoleGateway
    {
        public Exception? Failure { get; init; }

        public IReadOnlyCollection<long> DesiredRoleIds { get; private set; } = [];

        public IReadOnlyCollection<long> ManagedRoleIds { get; private set; } = [];

        public Task<DiscordRoleSnapshot> GetRoleAsync(
            long discordRoleId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DiscordRoleSnapshot> CreateRoleAsync(
            string name,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SynchronizeUserRolesAsync(
            long discordUserId,
            IReadOnlyCollection<long> desiredRoleIds,
            IReadOnlyCollection<long> managedRoleIds,
            CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            DesiredRoleIds = desiredRoleIds;
            ManagedRoleIds = managedRoleIds;
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
