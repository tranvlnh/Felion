using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class ReferenceDataManagementTests
{
    [Fact]
    public async Task AdminCanCreateDepartmentAndAuditDiscordActor()
    {
        var context = TestContext.Create(MemberPosition.Admin);
        var service = new DepartmentManagementService(context.ReferenceDataStore, context.MemberStore);

        var result = await service.CreateAsync(
            context.Actor.Id,
            new CreateDepartmentCommand("  Engineering  ", "  engineering "),
            "discord-command-1",
            CancellationToken.None,
            actorDiscordUserId: 123456789);

        Assert.Equal("Engineering", result.Name);
        Assert.Equal("engineering", result.Slug);
        var audit = Assert.Single(context.ReferenceDataStore.AuditLogs);
        Assert.Equal(AuditActorType.DiscordMember, audit.ActorType);
        Assert.Null(audit.ActorMemberId);
        Assert.Equal(123456789, audit.ActorDiscordUserId);
    }

    [Fact]
    public async Task CoreCannotCreateDepartment()
    {
        var context = TestContext.Create(MemberPosition.Core);
        var service = new DepartmentManagementService(context.ReferenceDataStore, context.MemberStore);

        await Assert.ThrowsAsync<ReferenceDataAccessDeniedException>(() => service.CreateAsync(
            context.Actor.Id,
            new CreateDepartmentCommand("Design", "design"),
            "correlation-2",
            CancellationToken.None));

        Assert.Empty(context.ReferenceDataStore.Departments);
        Assert.Empty(context.ReferenceDataStore.AuditLogs);
    }

    [Fact]
    public async Task DuplicateDepartmentSlugIsRejected()
    {
        var context = TestContext.Create(MemberPosition.Admin);
        context.ReferenceDataStore.Departments.Add(Department.CreateRegular("Existing", "engineering"));
        var service = new DepartmentManagementService(context.ReferenceDataStore, context.MemberStore);

        await Assert.ThrowsAsync<ReferenceDataConflictException>(() => service.CreateAsync(
            context.Actor.Id,
            new CreateDepartmentCommand("Another", "ENGINEERING"),
            "correlation-3",
            CancellationToken.None));

        Assert.Empty(context.ReferenceDataStore.AuditLogs);
    }

    [Fact]
    public async Task AdminCanCreateGenerationWithCanonicalCode()
    {
        var context = TestContext.Create(MemberPosition.Admin);
        var service = new GenerationManagementService(context.ReferenceDataStore, context.MemberStore);

        var result = await service.CreateAsync(
            context.Actor.Id,
            new CreateGenerationCommand("Generation 2026", " g26 "),
            "correlation-4",
            CancellationToken.None);

        Assert.Equal("G26", result.Code);
        var audit = Assert.Single(context.ReferenceDataStore.AuditLogs);
        Assert.Equal(AuditActorType.WebMember, audit.ActorType);
        Assert.Equal(context.Actor.Id, audit.ActorMemberId);
    }

    [Fact]
    public async Task CoreDepartmentSlugCannotBeCreatedAgain()
    {
        var context = TestContext.Create(MemberPosition.Admin);
        var service = new DepartmentManagementService(context.ReferenceDataStore, context.MemberStore);

        await Assert.ThrowsAsync<ReferenceDataValidationException>(() => service.CreateAsync(
            context.Actor.Id,
            new CreateDepartmentCommand("Another Core", "core"),
            "correlation-5",
            CancellationToken.None));

        Assert.Empty(context.ReferenceDataStore.AuditLogs);
    }

    private sealed class TestContext
    {
        private TestContext(Member actor, FakeMemberStore memberStore, FakeReferenceDataStore referenceDataStore)
        {
            Actor = actor;
            MemberStore = memberStore;
            ReferenceDataStore = referenceDataStore;
        }

        public Member Actor { get; }

        public FakeMemberStore MemberStore { get; }

        public FakeReferenceDataStore ReferenceDataStore { get; }

        public static TestContext Create(MemberPosition position)
        {
            var coreDepartment = Department.CreateCore();
            var generation = Generation.Create("Generation 1", "G1");
            var actor = Member.Create(
                "ADMIN001",
                "Admin",
                "admin@example.org",
                coreDepartment.Id,
                isCoreDepartment: true,
                generation.Id,
                position);
            var memberStore = new FakeMemberStore();
            memberStore.Members.Add(actor);
            return new TestContext(actor, memberStore, new FakeReferenceDataStore());
        }
    }

    private sealed class FakeReferenceDataStore : IReferenceDataStore
    {
        public List<Department> Departments { get; } = [];

        public List<Generation> Generations { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public Task<Department?> FindDepartmentBySlugAsync(
            string normalizedSlug,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Departments.SingleOrDefault(department => department.Slug == normalizedSlug));
        }

        public Task<Generation?> FindGenerationByCodeAsync(
            string normalizedCode,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Generations.SingleOrDefault(generation => generation.Code == normalizedCode));
        }

        public Task AddDepartmentAsync(
            Department department,
            AuditLog auditLog,
            CancellationToken cancellationToken)
        {
            Departments.Add(department);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddGenerationAsync(
            Generation generation,
            AuditLog auditLog,
            CancellationToken cancellationToken)
        {
            Generations.Add(generation);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }
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
