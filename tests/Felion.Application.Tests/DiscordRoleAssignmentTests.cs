using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Tests;

public sealed class DiscordRoleAssignmentTests
{
    [Fact]
    public async Task AdminCanReplaceAssignmentsAndSynchronizeLinkedSubjectImmediately()
    {
        var fixture = CreateFixture();
        var service = new DiscordRoleAssignmentService(
            fixture.Store,
            fixture.MemberStore,
            fixture.RoleCatalog,
            fixture.RoleSynchronizationService);

        var result = await service.ReplaceAsync(
            fixture.Admin.Id,
            DiscordIdentitySubjectType.Member,
            fixture.Subject.SubjectId,
            new UpdateDiscordRoleAssignmentsCommand([20]),
            "correlation-1",
            CancellationToken.None);

        Assert.Equal(["20"], result.Assignments.Select(assignment => assignment.DiscordRoleId));
        Assert.Equal([10L], fixture.Store.ExpectedRoleIds);
        Assert.Equal([20L], fixture.Store.SavedAssignments.Select(assignment => assignment.DiscordRoleId));
        Assert.Contains(
            (DiscordIdentitySubjectType.Member, fixture.Subject.SubjectId),
            fixture.RoleSynchronizationService.SynchronizedSubjects);
        Assert.Contains(fixture.Store.Audits, audit => audit.Action == "DiscordRoleAssignmentsUpdated");
    }

    [Fact]
    public async Task NonAdminCannotListRoleAssignments()
    {
        var fixture = CreateFixture();
        var service = new DiscordRoleAssignmentService(
            fixture.Store,
            fixture.MemberStore,
            fixture.RoleCatalog);

        await Assert.ThrowsAsync<DiscordRoleAssignmentAccessDeniedException>(() => service.ListAsync(
            fixture.RegularMember.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task ListIncludesAutomaticRolesSeparatelyFromIndividualAssignments()
    {
        var fixture = CreateFixture();
        var subject = fixture.Subject with
        {
            AutomaticRoles = [new DiscordRoleProjection(30, "Team role")]
        };
        var service = new DiscordRoleAssignmentService(
            new FakeRoleAssignmentStore(subject),
            fixture.MemberStore);

        var result = await service.ListAsync(fixture.Admin.Id, CancellationToken.None);

        var listedSubject = Assert.Single(result.Subjects);
        Assert.Equal(["10"], listedSubject.Assignments.Select(assignment => assignment.DiscordRoleId));
        Assert.Equal(["30"], listedSubject.AutomaticRoles.Select(role => role.DiscordRoleId));
    }

    [Fact]
    public async Task SavingSameAssignmentsRetriesSynchronizationForLinkedSubject()
    {
        var fixture = CreateFixture();
        var service = new DiscordRoleAssignmentService(
            fixture.Store,
            fixture.MemberStore,
            fixture.RoleCatalog,
            fixture.RoleSynchronizationService);

        await service.ReplaceAsync(
            fixture.Admin.Id,
            DiscordIdentitySubjectType.Member,
            fixture.Subject.SubjectId,
            new UpdateDiscordRoleAssignmentsCommand([10]),
            "correlation-retry-sync",
            CancellationToken.None);

        Assert.Contains(
            (DiscordIdentitySubjectType.Member, fixture.Subject.SubjectId),
            fixture.RoleSynchronizationService.SynchronizedSubjects);
    }

    [Fact]
    public async Task RoleCatalogExcludesEveryoneAndManagedRoles()
    {
        var fixture = CreateFixture();
        var service = new DiscordRoleAssignmentService(
            fixture.Store,
            fixture.MemberStore,
            fixture.RoleCatalog);

        var roles = await service.ListRolesAsync(fixture.Admin.Id, CancellationToken.None);

        Assert.Equal(["20", "10"], roles.Select(role => role.Id));
    }

    [Fact]
    public async Task RoleCatalogExcludesRolesTheBotCannotAssign()
    {
        var fixture = CreateFixture();
        var service = new DiscordRoleAssignmentService(
            fixture.Store,
            fixture.MemberStore,
            new FakeRoleCatalog([
                new DiscordGuildRoleSnapshot(20, "Too high", IsManaged: false, IsEveryone: false, RawPosition: 20, IsAssignableByBot: false),
                new DiscordGuildRoleSnapshot(10, "Assignable", IsManaged: false, IsEveryone: false, RawPosition: 10, IsAssignableByBot: true)]));

        var roles = await service.ListRolesAsync(fixture.Admin.Id, CancellationToken.None);

        Assert.Equal(["10"], roles.Select(role => role.Id));
    }

    [Fact]
    public async Task ReplaceRejectsRoleTheBotCannotAssign()
    {
        var fixture = CreateFixture();
        var service = new DiscordRoleAssignmentService(
            fixture.Store,
            fixture.MemberStore,
            new FakeRoleCatalog([
                new DiscordGuildRoleSnapshot(20, "Too high", IsManaged: false, IsEveryone: false, RawPosition: 20, IsAssignableByBot: false)]));

        await Assert.ThrowsAsync<DiscordRoleAssignmentValidationException>(() => service.ReplaceAsync(
            fixture.Admin.Id,
            DiscordIdentitySubjectType.Member,
            fixture.Subject.SubjectId,
            new UpdateDiscordRoleAssignmentsCommand([20]),
            "correlation-role-hierarchy",
            CancellationToken.None));
    }

    private static Fixture CreateFixture()
    {
        var core = Department.CreateCore();
        var regularDepartment = Department.CreateRegular("Technical", "technical");
        var generation = Generation.Create("Generation 1", "G1");
        var admin = Member.Create(
            "ADMIN001",
            "Admin",
            "admin@gdscptit.dev",
            core.Id,
            isCoreDepartment: true,
            generation.Id,
            MemberPosition.Admin);
        var regularMember = Member.Create(
            "MEMBER001",
            "Member",
            "member@gdscptit.dev",
            regularDepartment.Id,
            isCoreDepartment: false,
            generation.Id,
            MemberPosition.Member);
        var existingAssignment = DiscordRoleAssignment.Create(
            DiscordIdentitySubjectType.Member,
            regularMember.Id,
            10,
            "Old role");
        var subject = new DiscordRoleAssignmentSubjectView(
            regularMember.Id,
            DiscordIdentitySubjectType.Member,
            regularMember.StudentId,
            regularMember.FullName,
            regularMember.Position,
            regularMember.Status,
            CandidateStatus: null,
            DiscordUserId: 123456789,
            [existingAssignment],
            AutomaticRoles: []);
        return new Fixture(
            admin,
            regularMember,
            subject,
            new FakeMemberStore(admin, regularMember),
            new FakeRoleAssignmentStore(subject),
            new FakeRoleCatalog([
                new DiscordGuildRoleSnapshot(99, "@everyone", IsManaged: false, IsEveryone: true, RawPosition: 0),
                new DiscordGuildRoleSnapshot(30, "Integration", IsManaged: true, IsEveryone: false, RawPosition: 10),
                new DiscordGuildRoleSnapshot(20, "New role", IsManaged: false, IsEveryone: false, RawPosition: 20),
                new DiscordGuildRoleSnapshot(10, "Old role", IsManaged: false, IsEveryone: false, RawPosition: 19)]),
            new TestDiscordRoleSynchronizationService());
    }

    private sealed record Fixture(
        Member Admin,
        Member RegularMember,
        DiscordRoleAssignmentSubjectView Subject,
        FakeMemberStore MemberStore,
        FakeRoleAssignmentStore Store,
        FakeRoleCatalog RoleCatalog,
        TestDiscordRoleSynchronizationService RoleSynchronizationService);

    private sealed class FakeRoleAssignmentStore(DiscordRoleAssignmentSubjectView subject)
        : IDiscordRoleAssignmentStore
    {
        public DiscordRoleAssignmentSubjectView Subject { get; } = subject;

        public IReadOnlyCollection<long> ExpectedRoleIds { get; private set; } = [];

        public IReadOnlyCollection<DiscordRoleAssignment> SavedAssignments { get; private set; } = [];

        public List<AuditLog> Audits { get; } = [];

        public Task<IReadOnlyList<DiscordRoleAssignmentSubjectView>> ListSubjectsAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DiscordRoleAssignmentSubjectView>>([Subject]);

        public Task<DiscordRoleAssignmentSubjectView?> FindSubjectAsync(
            DiscordIdentitySubjectType subjectType,
            Guid subjectId,
            CancellationToken cancellationToken)
            => Task.FromResult<DiscordRoleAssignmentSubjectView?>(
                Subject.SubjectType == subjectType && Subject.SubjectId == subjectId ? Subject : null);

        public Task ReplaceAsync(
            DiscordIdentitySubjectType subjectType,
            Guid subjectId,
            IReadOnlyCollection<long> expectedRoleIds,
            IReadOnlyCollection<DiscordRoleAssignment> desiredAssignments,
            AuditLog auditLog,
            CancellationToken cancellationToken)
        {
            ExpectedRoleIds = expectedRoleIds;
            SavedAssignments = desiredAssignments;
            Audits.Add(auditLog);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoleCatalog(IReadOnlyList<DiscordGuildRoleSnapshot> roles)
        : IDiscordGuildRoleCatalog
    {
        public Task<IReadOnlyList<DiscordGuildRoleSnapshot>> ListRolesAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DiscordGuildRoleSnapshot>>(roles);
    }

    private sealed class FakeMemberStore(Member admin, Member regularMember) : IMemberStore
    {
        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
            => Task.FromResult<Member?>(memberId == admin.Id ? admin : memberId == regularMember.Id ? regularMember : null);

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
