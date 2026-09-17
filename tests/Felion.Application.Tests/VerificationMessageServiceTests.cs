using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class VerificationMessageServiceTests
{
    [Fact]
    public async Task AdminCanPublishVerificationMessageAndAuditDiscordActor()
    {
        var context = TestContext.Create(MemberPosition.Admin);
        var gateway = new FakeVerificationMessageGateway();
        var service = new DiscordVerificationMessageService(
            context.Store,
            context.MemberStore,
            gateway);

        var result = await service.PublishAsync(
            context.Actor.Id,
            456789123,
            "discord-command-1",
            CancellationToken.None,
            actorDiscordUserId: 123456789);

        Assert.Equal(456789123, result.ChannelId);
        Assert.Equal(987654321, result.MessageId);
        Assert.Equal(456789123, gateway.PublishedChannelId);
        var audit = Assert.Single(context.Store.AuditLogs);
        Assert.Equal(AuditActorType.DiscordMember, audit.ActorType);
        Assert.Null(audit.ActorMemberId);
        Assert.Equal(123456789, audit.ActorDiscordUserId);
        Assert.Equal("DiscordVerificationMessagePublished", audit.Action);
    }

    [Fact]
    public async Task CoreCannotPublishVerificationMessage()
    {
        var context = TestContext.Create(MemberPosition.Core);
        var gateway = new FakeVerificationMessageGateway();
        var service = new DiscordVerificationMessageService(
            context.Store,
            context.MemberStore,
            gateway);

        await Assert.ThrowsAsync<DiscordVerificationMessageAccessDeniedException>(() => service.PublishAsync(
            context.Actor.Id,
            456789123,
            "discord-command-2",
            CancellationToken.None));

        Assert.Null(gateway.PublishedChannelId);
        Assert.Empty(context.Store.AuditLogs);
    }

    [Fact]
    public async Task PublishingWithoutDiscordGatewayIsRejected()
    {
        var context = TestContext.Create(MemberPosition.Admin);
        var service = new DiscordVerificationMessageService(
            context.Store,
            context.MemberStore);

        await Assert.ThrowsAsync<DiscordVerificationMessageUnavailableException>(() => service.PublishAsync(
            context.Actor.Id,
            456789123,
            "discord-command-3",
            CancellationToken.None));

        Assert.Empty(context.Store.AuditLogs);
    }

    [Fact]
    public async Task UnlinkedServerAdministratorCanPublishVerificationMessage()
    {
        var context = TestContext.Create(MemberPosition.Admin);
        var gateway = new FakeVerificationMessageGateway();
        var permissionGateway = new FakeGuildPermissionGateway { IsAdministrator = true };
        var service = new DiscordVerificationMessageService(
            context.Store,
            context.MemberStore,
            gateway,
            new FakeDiscordAuthorizationService(),
            permissionGateway);

        var result = await service.PublishFromDiscordAsync(
            123456789,
            456789123,
            "discord-bootstrap-command-1",
            CancellationToken.None);

        Assert.Equal(987654321, result.MessageId);
        var audit = Assert.Single(context.Store.AuditLogs);
        Assert.Equal(AuditActorType.DiscordMember, audit.ActorType);
        Assert.Null(audit.ActorMemberId);
        Assert.Equal(123456789, audit.ActorDiscordUserId);
        Assert.Contains("DiscordServerAdministrator", audit.AfterJson);
    }

    [Fact]
    public async Task UnlinkedNonAdministratorCannotPublishVerificationMessage()
    {
        var context = TestContext.Create(MemberPosition.Admin);
        var gateway = new FakeVerificationMessageGateway();
        var service = new DiscordVerificationMessageService(
            context.Store,
            context.MemberStore,
            gateway,
            new FakeDiscordAuthorizationService(),
            new FakeGuildPermissionGateway());

        await Assert.ThrowsAsync<DiscordVerificationMessageAccessDeniedException>(() =>
            service.PublishFromDiscordAsync(
                123456789,
                456789123,
                "discord-bootstrap-command-2",
                CancellationToken.None));

        Assert.Null(gateway.PublishedChannelId);
        Assert.Empty(context.Store.AuditLogs);
    }

    private sealed class TestContext
    {
        private TestContext(Member actor, FakeMemberStore memberStore, FakeVerificationMessageStore store)
        {
            Actor = actor;
            MemberStore = memberStore;
            Store = store;
        }

        public Member Actor { get; }

        public FakeMemberStore MemberStore { get; }

        public FakeVerificationMessageStore Store { get; }

        public static TestContext Create(MemberPosition position)
        {
            var department = Department.CreateCore();
            var generation = Generation.Create("Generation 1", "G1");
            var actor = Member.Create(
                "ADMIN001",
                "Admin",
                "admin@example.org",
                department.Id,
                isCoreDepartment: true,
                generation.Id,
                position);
            var memberStore = new FakeMemberStore();
            memberStore.Members.Add(actor);
            return new TestContext(actor, memberStore, new FakeVerificationMessageStore());
        }
    }

    private sealed class FakeVerificationMessageGateway : IDiscordVerificationMessageGateway
    {
        public long? PublishedChannelId { get; private set; }

        public Task<DiscordVerificationMessageSnapshot> PublishAsync(
            long channelId,
            CancellationToken cancellationToken)
        {
            PublishedChannelId = channelId;
            return Task.FromResult(new DiscordVerificationMessageSnapshot(channelId, 987654321));
        }
    }

    private sealed class FakeVerificationMessageStore : IDiscordVerificationMessageStore
    {
        public List<AuditLog> AuditLogs { get; } = [];

        public Task AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGuildPermissionGateway : IDiscordGuildPermissionGateway
    {
        public bool IsAdministrator { get; init; }

        public Task<bool> IsAdministratorAsync(
            long discordUserId,
            CancellationToken cancellationToken)
            => Task.FromResult(IsAdministrator);
    }

    private sealed class FakeDiscordAuthorizationService : IDiscordAuthorizationService
    {
        public Task<DiscordAdminActor?> FindAdminAsync(
            long discordUserId,
            CancellationToken cancellationToken)
            => Task.FromResult<DiscordAdminActor?>(null);

        public Task<DiscordAdminActor> RequireAdminAsync(
            long discordUserId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<DiscordManagementActor?> FindCoreOrAdminAsync(
            long discordUserId,
            CancellationToken cancellationToken)
            => Task.FromResult<DiscordManagementActor?>(null);

        public Task<DiscordManagementActor> RequireCoreOrAdminAsync(
            long discordUserId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeMemberStore : IMemberStore
    {
        public List<Member> Members { get; } = [];

        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Members.SingleOrDefault(member => member.Id == memberId));
        }

        public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(
            int page,
            int pageSize,
            MemberStatus? status,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(
            IReadOnlyCollection<string> studentIds,
            IReadOnlyCollection<string> clubEmails,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task UpdateAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddRangeAsync(
            IReadOnlyCollection<MemberAuditEntry> entries,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
