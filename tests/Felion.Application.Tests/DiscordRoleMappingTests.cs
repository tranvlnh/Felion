using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Tests;

public sealed class DiscordRoleMappingTests
{
    [Fact]
    public async Task AdminCanCreateMappingWithCanonicalPositionKeyAndAudit()
    {
        var store = CreateStore(out var actor);
        var mappingStore = new FakeMappingStore();
        var roleSynchronizationService = new TestDiscordRoleSynchronizationService();
        var service = new DiscordRoleMappingService(mappingStore, store, null, roleSynchronizationService);

        var result = await service.UpsertAsync(
            actor.Id,
            new UpsertDiscordRoleMappingCommand(
                DiscordRoleMappingKind.Position,
                " member ",
                123456789,
                "Members"),
            "correlation-1",
            CancellationToken.None);

        Assert.Equal("Member", result.SubjectKey);
        Assert.Equal(123456789, result.DiscordRoleId);
        Assert.Single(mappingStore.Mappings);
        Assert.Equal(1, roleSynchronizationService.SyncAllCalls);
        Assert.Contains(mappingStore.AuditLogs, audit => audit.Action == "DiscordRoleMappingUpserted");
    }

    [Fact]
    public async Task CoreCannotManageRoleMappings()
    {
        var store = CreateStore(out _);
        var core = Member.Create(
            "CORE001",
            "Core",
            "core@example.org",
            store.CoreDepartment.Id,
            isCoreDepartment: true,
            store.Generation.Id,
            MemberPosition.Core);
        store.Members.Add(core);
        var service = new DiscordRoleMappingService(new FakeMappingStore(), store);

        await Assert.ThrowsAsync<DiscordRoleMappingAccessDeniedException>(() => service.ListAsync(
            core.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task InvalidRoleMappingIsRejectedBeforePersistence()
    {
        var store = CreateStore(out var actor);
        var mappingStore = new FakeMappingStore();
        var service = new DiscordRoleMappingService(mappingStore, store);

        await Assert.ThrowsAsync<DiscordRoleMappingValidationException>(() => service.UpsertAsync(
            actor.Id,
            new UpsertDiscordRoleMappingCommand(
                DiscordRoleMappingKind.Position,
                "NotAPosition",
                123456789,
                "Role"),
            "correlation-2",
            CancellationToken.None));

        Assert.Empty(mappingStore.Mappings);
        Assert.Empty(mappingStore.AuditLogs);
    }

    [Fact]
    public async Task UpsertUpdatesExistingLogicalMapping()
    {
        var store = CreateStore(out var actor);
        var mappingStore = new FakeMappingStore();
        var existing = DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Probation,
            "Probation",
            123456789,
            "Old Probation");
        mappingStore.Mappings.Add(existing);
        var roleSynchronizationService = new TestDiscordRoleSynchronizationService();
        var service = new DiscordRoleMappingService(mappingStore, store, null, roleSynchronizationService);

        var result = await service.UpsertAsync(
            actor.Id,
            new UpsertDiscordRoleMappingCommand(
                DiscordRoleMappingKind.Probation,
                "probation",
                987654321,
                "Probation"),
            "correlation-3",
            CancellationToken.None);

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(987654321, result.DiscordRoleId);
        Assert.Single(mappingStore.Mappings);
        Assert.NotNull(mappingStore.AuditLogs[0].BeforeJson);
        Assert.Equal(1, roleSynchronizationService.SyncAllCalls);
    }

    [Fact]
    public async Task UpsertSynchronizesAllLinkedSubjectsAffectedByMapping()
    {
        var store = CreateStore(out var actor);
        var mappingStore = new FakeMappingStore();
        var roleSynchronizationService = new TestDiscordRoleSynchronizationService();
        var service = new DiscordRoleMappingService(mappingStore, store, null, roleSynchronizationService);

        await service.UpsertAsync(
            actor.Id,
            new UpsertDiscordRoleMappingCommand(
                DiscordRoleMappingKind.Probation,
                "Probation",
                123456789,
                "Probation"),
            "correlation-sync-mapping",
            CancellationToken.None);

        Assert.Equal(1, roleSynchronizationService.SyncAllCalls);
    }

    [Fact]
    public async Task AdminCanCreateDiscordRoleAndAuditTheMutation()
    {
        var store = CreateStore(out var actor);
        var mappingStore = new FakeMappingStore();
        var roleGateway = new FakeRoleGateway
        {
            CreatedRole = new DiscordRoleSnapshot(987654321, "Core")
        };
        var service = new DiscordRoleManagementService(mappingStore, store, roleGateway);

        var result = await service.CreateAsync(
            actor.Id,
            new CreateDiscordRoleCommand(" Core "),
            "correlation-create",
            CancellationToken.None,
            actorDiscordUserId: 123456789);

        Assert.Equal(987654321, result.Id);
        Assert.Equal("Core", result.Name);
        var audit = Assert.Single(mappingStore.AuditLogs, audit => audit.Action == "DiscordRoleCreated");
        Assert.Equal(AuditActorType.DiscordMember, audit.ActorType);
        Assert.Null(audit.ActorMemberId);
        Assert.Equal(123456789, audit.ActorDiscordUserId);
    }

    [Fact]
    public async Task CoreCannotCreateDiscordRole()
    {
        var store = CreateStore(out _);
        var core = Member.Create(
            "CORE002",
            "Core",
            "core2@example.org",
            store.CoreDepartment.Id,
            isCoreDepartment: true,
            store.Generation.Id,
            MemberPosition.Core);
        store.Members.Add(core);
        var service = new DiscordRoleManagementService(
            new FakeMappingStore(),
            store,
            new FakeRoleGateway());

        await Assert.ThrowsAsync<DiscordRoleMappingAccessDeniedException>(() => service.CreateAsync(
            core.Id,
            new CreateDiscordRoleCommand("Role"),
            "correlation-denied",
            CancellationToken.None));
    }

    [Fact]
    public async Task ExistingRoleMappingUsesTheDiscordRoleNameSnapshot()
    {
        var store = CreateStore(out var actor);
        var mappingStore = new FakeMappingStore();
        var roleGateway = new FakeRoleGateway
        {
            ExistingRole = new DiscordRoleSnapshot(123456789, "Actual role name")
        };
        var service = new DiscordRoleMappingService(mappingStore, store, roleGateway);

        var result = await service.UpsertAsync(
            actor.Id,
            new UpsertDiscordRoleMappingCommand(
                DiscordRoleMappingKind.Position,
                "Member",
                123456789,
                "Untrusted snapshot"),
            "correlation-role-lookup",
            CancellationToken.None);

        Assert.Equal("Actual role name", result.RoleNameSnapshot);
    }

    [Fact]
    public async Task RoleMappingSubjectResolverResolvesReferenceDataAndTeamNames()
    {
        var store = CreateStore(out _);
        var department = Department.CreateRegular("Engineering", "engineering");
        var team = ProbationTeam.Create("Spring 2026");
        store.Departments.Add(department);
        var teamStore = new FakeProbationTeamStore();
        teamStore.Teams.Add(new ProbationTeamView(
            team.Id,
            team.Name,
            team.IsActive,
            [],
            [],
            team.CreatedAt,
            team.UpdatedAt));
        var resolver = new DiscordRoleMappingSubjectResolver(store, teamStore);

        var departmentKey = await resolver.ResolveAsync(
            DiscordRoleMappingKind.Department,
            " engineering ",
            CancellationToken.None);
        var generationKey = await resolver.ResolveAsync(
            DiscordRoleMappingKind.Generation,
            "Generation 1",
            CancellationToken.None);
        var teamKey = await resolver.ResolveAsync(
            DiscordRoleMappingKind.ProbationTeam,
            "spring 2026",
            CancellationToken.None);

        Assert.Equal(department.Id.ToString("D"), departmentKey);
        Assert.Equal(store.Generation.Id.ToString("D"), generationKey);
        Assert.Equal(team.Id.ToString("D"), teamKey);
    }

    [Fact]
    public async Task UnknownRoleMappingSubjectNameIsRejected()
    {
        var store = CreateStore(out _);
        var resolver = new DiscordRoleMappingSubjectResolver(store, new FakeProbationTeamStore());

        await Assert.ThrowsAsync<DiscordRoleMappingValidationException>(() => resolver.ResolveAsync(
            DiscordRoleMappingKind.Department,
            "Missing Department",
            CancellationToken.None));
    }

    [Fact]
    public async Task AmbiguousRoleMappingSubjectNameIsRejected()
    {
        var store = CreateStore(out _);
        store.Departments.Add(Department.CreateRegular("Product", "product"));
        store.Departments.Add(Department.CreateRegular("Product", "product-2"));
        var resolver = new DiscordRoleMappingSubjectResolver(store, new FakeProbationTeamStore());

        await Assert.ThrowsAsync<DiscordRoleMappingValidationException>(() => resolver.ResolveAsync(
            DiscordRoleMappingKind.Department,
            "Product",
            CancellationToken.None));
    }

    private static FakeMemberStore CreateStore(out Member actor)
    {
        var store = new FakeMemberStore
        {
            CoreDepartment = Department.CreateCore(),
            Generation = Generation.Create("Generation 1", "G1")
        };
        store.Departments.Add(store.CoreDepartment);
        store.Generations.Add(store.Generation);
        actor = Member.Create(
            "ADMIN001",
            "Admin",
            "admin@example.org",
            store.CoreDepartment.Id,
            isCoreDepartment: true,
            store.Generation.Id,
            MemberPosition.Admin);
        store.Members.Add(actor);
        return store;
    }

    private sealed class FakeMappingStore : IDiscordRoleMappingStore, IDiscordRoleManagementStore
    {
        public List<DiscordRoleMapping> Mappings { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public Task AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DiscordRoleMapping>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<DiscordRoleMapping>>(Mappings.ToArray());
        }

        public Task<DiscordRoleMapping?> FindAsync(
            DiscordRoleMappingKind kind,
            string subjectKey,
            bool track,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Mappings.SingleOrDefault(mapping =>
                mapping.Kind == kind && mapping.SubjectKey == subjectKey));
        }

        public Task UpsertAsync(
            DiscordRoleMapping mapping,
            AuditLog auditLog,
            CancellationToken cancellationToken)
        {
            if (!Mappings.Contains(mapping))
            {
                Mappings.Add(mapping);
            }

            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoleGateway : IDiscordRoleGateway
    {
        public DiscordRoleSnapshot? ExistingRole { get; init; }

        public DiscordRoleSnapshot CreatedRole { get; init; } = new(1, "Created role");

        public Task<DiscordRoleSnapshot> GetRoleAsync(
            long discordRoleId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ExistingRole
                ?? throw new DiscordRoleNotFoundException("Role was not found."));
        }

        public Task<DiscordRoleSnapshot> CreateRoleAsync(
            string name,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CreatedRole);
        }

        public Task SynchronizeUserRolesAsync(
            long discordUserId,
            IReadOnlyCollection<long> desiredRoleIds,
            IReadOnlyCollection<long> managedRoleIds,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMemberStore : IMemberStore
    {
        public List<Member> Members { get; } = [];

        public List<Department> Departments { get; } = [];

        public List<Generation> Generations { get; } = [];

        public required Department CoreDepartment { get; init; }

        public required Generation Generation { get; init; }

        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Members.SingleOrDefault(member => member.Id == memberId));
        }

        public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(
            int page,
            int pageSize,
            MemberStatus? status,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(
            IReadOnlyCollection<string> studentIds,
            IReadOnlyCollection<string> clubEmails,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Department>>(Departments.ToArray());
        }

        public Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Generation>>(Generations.ToArray());
        }

        public Task AddAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task AddRangeAsync(IReadOnlyCollection<MemberAuditEntry> entries, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeProbationTeamStore : IProbationTeamStore
    {
        public List<ProbationTeamView> Teams { get; } = [];

        public Task<IReadOnlyList<ProbationTeamView>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ProbationTeamView>>(Teams.ToArray());

        public Task<ProbationTeamView?> FindViewAsync(Guid teamId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ProbationTeam?> FindTeamAsync(Guid teamId, bool track, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ProbationCandidate?> FindCandidateAsync(Guid candidateId, bool track, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<TeamMentor?> FindMentorAsync(Guid teamId, Guid memberId, bool track, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<long?> FindCandidateDiscordUserIdAsync(Guid candidateId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddTeamAsync(ProbationTeam team, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task UpdateTeamAsync(ProbationTeam team, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task SaveCandidateAssignmentAsync(
            ProbationCandidate candidate,
            AuditLog auditLog,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddMentorAsync(TeamMentor mentor, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task RemoveMentorAsync(TeamMentor mentor, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
