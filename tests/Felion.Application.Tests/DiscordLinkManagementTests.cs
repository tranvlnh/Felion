using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class DiscordLinkManagementTests
{
    [Fact]
    public async Task UnlinkRemovesLinkAuditsAndClearsManagedRolesImmediately()
    {
        var fixture = CreateFixture();
        var link = DiscordIdentityLink.Create(123456789, "MEM001", DiscordIdentitySubjectType.Member, fixture.Member.Id);
        fixture.Store.Links.Add(link);
        var service = new DiscordLinkManagementService(
            fixture.Store,
            fixture.Store,
            fixture.RoleSynchronizationService);

        var result = await service.UnlinkAsync(
            fixture.Admin.Id,
            fixture.Member.Id,
            "correlation-unlink",
            CancellationToken.None);

        Assert.Equal(123456789, result.DiscordUserId);
        Assert.Empty(fixture.Store.Links);
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "DiscordIdentityUnlinked");
        Assert.Contains(
            (DiscordIdentitySubjectType.Member, fixture.Member.Id, 123456789L),
            fixture.RoleSynchronizationService.ClearedSubjects);
    }

    [Fact]
    public async Task RelinkTransfersIdentityAndSynchronizesImmediately()
    {
        var fixture = CreateFixture();
        var link = DiscordIdentityLink.Create(123456789, "MEM001", DiscordIdentitySubjectType.Member, fixture.Member.Id);
        fixture.Store.Links.Add(link);
        var service = new DiscordLinkManagementService(
            fixture.Store,
            fixture.Store,
            fixture.RoleSynchronizationService);

        var result = await service.RelinkAsync(
            fixture.Admin.Id,
            fixture.Member.Id,
            new RelinkDiscordCommand(987654321),
            "correlation-relink",
            CancellationToken.None);

        Assert.Equal(123456789, result.PreviousDiscordUserId);
        Assert.Equal(987654321, result.DiscordUserId);
        Assert.Equal(987654321, Assert.Single(fixture.Store.Links).DiscordUserId);
        Assert.Contains(
            (DiscordIdentitySubjectType.Member, fixture.Member.Id, 123456789L),
            fixture.RoleSynchronizationService.ClearedSubjects);
        Assert.Contains(
            (DiscordIdentitySubjectType.Member, fixture.Member.Id),
            fixture.RoleSynchronizationService.SynchronizedSubjects);
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "DiscordIdentityRelinked");
    }

    [Fact]
    public async Task ForceSyncSynchronizesAndAuditsAnActiveMember()
    {
        var fixture = CreateFixture();
        fixture.Store.Links.Add(
            DiscordIdentityLink.Create(123456789, "MEM001", DiscordIdentitySubjectType.Member, fixture.Member.Id));
        var service = new DiscordLinkManagementService(
            fixture.Store,
            fixture.Store,
            fixture.RoleSynchronizationService);

        var result = await service.ForceSyncAsync(
            fixture.Admin.Id,
            fixture.Member.Id,
            "correlation-force-sync",
            CancellationToken.None);

        Assert.True(result.RolesSynchronized);
        Assert.Contains(
            (DiscordIdentitySubjectType.Member, fixture.Member.Id),
            fixture.RoleSynchronizationService.SynchronizedSubjects);
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "DiscordRoleSyncRequested");
    }

    [Fact]
    public async Task RegularMemberCannotManageDiscordLinks()
    {
        var fixture = CreateFixture();
        var service = new DiscordLinkManagementService(fixture.Store, fixture.Store);

        await Assert.ThrowsAsync<DiscordLinkManagementAccessDeniedException>(() => service.ForceSyncAsync(
            fixture.Member.Id,
            fixture.Member.Id,
            "correlation-denied",
            CancellationToken.None));
    }

    [Fact]
    public async Task CoreCanForceSyncDiscordLink()
    {
        var fixture = CreateFixture();
        fixture.Store.Links.Add(
            DiscordIdentityLink.Create(123456789, "MEM001", DiscordIdentitySubjectType.Member, fixture.Member.Id));
        var service = new DiscordLinkManagementService(
            fixture.Store,
            fixture.Store,
            fixture.RoleSynchronizationService);

        var result = await service.ForceSyncAsync(
            fixture.Core.Id,
            fixture.Member.Id,
            "correlation-core-sync",
            CancellationToken.None);

        Assert.True(result.RolesSynchronized);
        Assert.Contains(
            (DiscordIdentitySubjectType.Member, fixture.Member.Id),
            fixture.RoleSynchronizationService.SynchronizedSubjects);
    }

    private static Fixture CreateFixture()
    {
        var store = new FakeStore();
        store.CoreDepartment = Department.CreateCore();
        store.Department = Department.CreateRegular("Engineering", "engineering");
        store.Generation = Generation.Create("Generation 1", "G1");
        store.Admin = Member.Create(
            "ADMIN001",
            "Admin",
            "admin@example.org",
            store.CoreDepartment.Id,
            isCoreDepartment: true,
            store.Generation.Id,
            MemberPosition.Admin);
        store.Core = Member.Create(
            "CORE001",
            "Core",
            "core@example.org",
            store.CoreDepartment.Id,
            isCoreDepartment: true,
            store.Generation.Id,
            MemberPosition.Core);
        store.Member = Member.Create(
            "MEM001",
            "Member",
            "member@example.org",
            store.Department.Id,
            isCoreDepartment: false,
            store.Generation.Id,
            MemberPosition.Member);
        store.Members.AddRange([store.Admin, store.Core, store.Member]);
        return new Fixture(store, store.Admin, store.Core, store.Member, new TestDiscordRoleSynchronizationService());
    }

    private sealed record Fixture(
        FakeStore Store,
        Member Admin,
        Member Core,
        Member Member,
        TestDiscordRoleSynchronizationService RoleSynchronizationService);

    private sealed class FakeStore : IMemberStore, IDiscordLinkManagementStore
    {
        public List<Member> Members { get; } = [];

        public List<DiscordIdentityLink> Links { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public Department CoreDepartment { get; set; } = null!;

        public Department Department { get; set; } = null!;

        public Generation Generation { get; set; } = null!;

        public Member Admin { get; set; } = null!;

        public Member Core { get; set; } = null!;

        public Member Member { get; set; } = null!;

        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Members.SingleOrDefault(member => member.Id == memberId));
        }

        public Task<DiscordIdentityLink?> FindByMemberIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Links.SingleOrDefault(link => link.SubjectId == memberId));
        }

        public Task<DiscordIdentityLink?> FindByDiscordUserIdAsync(long discordUserId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Links.SingleOrDefault(link => link.DiscordUserId == discordUserId));
        }

        public Task UnlinkAsync(DiscordIdentityLink link, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Links.Remove(link);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task RelinkAsync(DiscordIdentityLink link, AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task RecordAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(int page, int pageSize, MemberStatus? status, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(IReadOnlyCollection<string> studentIds, IReadOnlyCollection<string> clubEmails, CancellationToken cancellationToken)
            => throw new NotSupportedException();

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

        public Task AddRangeAsync(IReadOnlyCollection<MemberAuditEntry> entries, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
