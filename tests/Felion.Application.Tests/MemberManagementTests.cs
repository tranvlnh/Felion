using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class MemberManagementTests
{
    [Fact]
    public async Task CreateRequiresAnActiveCoreOrAdminActor()
    {
        var store = CreateStore();
        var actor = CreateActor(store);
        actor.Deactivate();
        var service = new MemberManagementService(store);

        await Assert.ThrowsAsync<MemberAccessDeniedException>(() => service.CreateAsync(
            actor.Id,
            new CreateMemberCommand(
                "SV001",
                "Student One",
                "student@example.org",
                store.RegularDepartment.Id,
                store.Generations[0].Id,
                MemberPosition.Member),
            "correlation-1",
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateNormalizesDataAndWritesAudit()
    {
        var store = CreateStore();
        var actor = CreateActor(store);
        var service = new MemberManagementService(store);

        var result = await service.CreateAsync(
            actor.Id,
            new CreateMemberCommand(
                " sv001 ",
                " Student One ",
                " STUDENT@EXAMPLE.ORG ",
                store.RegularDepartment.Id,
                store.Generations[0].Id,
                MemberPosition.Member),
            "correlation-1",
            CancellationToken.None);

        Assert.Equal("SV001", result.StudentId);
        Assert.Equal("student@example.org", result.ClubEmail);
        Assert.Contains(store.AuditLogs, audit => audit.Action == "MemberCreated" && audit.EntityId == result.Id);
    }

    [Fact]
    public async Task ImportDuplicateRowReturnsErrorsAndDoesNotCommitAnyRow()
    {
        var store = CreateStore();
        var actor = CreateActor(store);
        var service = new MemberManagementService(store);
        var readResult = new MemberImportReadResult(
        [
            new MemberImportRow(2, "SV002", "Student Two", "two@example.org", store.RegularDepartment.Slug, "G1", "Member"),
            new MemberImportRow(3, " sv002 ", "Student Three", "three@example.org", store.RegularDepartment.Slug, "G1", "Member")
        ],
        []);

        var report = await service.ImportAsync(
            actor.Id,
            "members.csv",
            readResult,
            "correlation-2",
            CancellationToken.None);

        Assert.False(report.Committed);
        Assert.Contains(report.Errors, error => error.RowNumber == 3 && error.Field == nameof(MemberImportRow.StudentId));
        Assert.Single(store.Members);
        Assert.Empty(store.AuditLogs);
    }

    [Fact]
    public async Task ImportRejectsCorePositionInRegularDepartment()
    {
        var store = CreateStore();
        var actor = CreateActor(store);
        var service = new MemberManagementService(store);
        var readResult = new MemberImportReadResult(
            [new MemberImportRow(2, "SV002", "Student Two", "two@example.org", store.RegularDepartment.Slug, "G1", "Core")],
            []);

        var report = await service.ImportAsync(
            actor.Id,
            "members.csv",
            readResult,
            "correlation-3",
            CancellationToken.None);

        Assert.False(report.Committed);
        Assert.Contains(report.Errors, error => error.RowNumber == 2 && error.Field == "Member");
        Assert.Single(store.Members);
    }

    [Fact]
    public async Task ImportExistingStudentIdReturnsErrorAndDoesNotCommit()
    {
        var store = CreateStore();
        var actor = CreateActor(store);
        store.Members.Add(Member.Create(
            "SV002",
            "Existing Student",
            "existing@example.org",
            store.RegularDepartment.Id,
            isCoreDepartment: false,
            store.Generations[0].Id,
            MemberPosition.Member));
        var service = new MemberManagementService(store);
        var readResult = new MemberImportReadResult(
            [new MemberImportRow(2, "SV002", "Student Two", "two@example.org", store.RegularDepartment.Slug, "G1", "Member")],
            []);

        var report = await service.ImportAsync(
            actor.Id,
            "members.csv",
            readResult,
            "correlation-4",
            CancellationToken.None);

        Assert.False(report.Committed);
        Assert.Contains(report.Errors, error => error.RowNumber == 2 && error.Field == nameof(MemberImportRow.StudentId));
        Assert.Equal(2, store.Members.Count);
    }

    private static FakeMemberStore CreateStore()
    {
        var store = new FakeMemberStore
        {
            CoreDepartment = Department.CreateCore(),
            RegularDepartment = Department.CreateRegular("Technical", "technical")
        };
        store.Departments.Add(store.CoreDepartment);
        store.Departments.Add(store.RegularDepartment);
        store.Generations.Add(Generation.Create("Generation 1", "G1"));
        return store;
    }

    private static Member CreateActor(FakeMemberStore store)
    {
        var actor = Member.Create(
            "ADMIN001",
            "Admin",
            "admin@example.org",
            store.CoreDepartment.Id,
            isCoreDepartment: true,
            store.Generations[0].Id,
            MemberPosition.Admin);
        store.Members.Add(actor);
        return actor;
    }

    private sealed class FakeMemberStore : IMemberStore
    {
        public List<Member> Members { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public List<Department> Departments { get; } = [];

        public List<Generation> Generations { get; } = [];

        public required Department CoreDepartment { get; init; }

        public required Department RegularDepartment { get; init; }

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
            var query = Members.AsEnumerable();
            if (status is not null)
            {
                query = query.Where(member => member.Status == status.Value);
            }

            var items = query.OrderBy(member => member.StudentId).Skip((page - 1) * pageSize).Take(pageSize).ToArray();
            return Task.FromResult<(IReadOnlyList<Member>, int)>((items, query.Count()));
        }

        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(
            IReadOnlyCollection<string> studentIds,
            IReadOnlyCollection<string> clubEmails,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new MemberIdentityLookup(
                Members.Where(member => studentIds.Contains(member.StudentId)).Select(member => member.StudentId).ToHashSet(),
                Members.Where(member => clubEmails.Contains(member.ClubEmail)).Select(member => member.ClubEmail).ToHashSet()));
        }

        public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Departments.SingleOrDefault(department => department.Id == departmentId));
        }

        public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Generations.SingleOrDefault(generation => generation.Id == generationId));
        }

        public Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Department>>(Departments);
        }

        public Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Generation>>(Generations);
        }

        public Task AddAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
        {
            EnsureUnique(member, null);
            Members.Add(member);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken)
        {
            EnsureUnique(member, member.Id);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IReadOnlyCollection<MemberAuditEntry> entries, CancellationToken cancellationToken)
        {
            foreach (var entry in entries)
            {
                EnsureUnique(entry.Member, null);
            }

            Members.AddRange(entries.Select(entry => entry.Member));
            AuditLogs.AddRange(entries.Select(entry => entry.AuditLog));
            return Task.CompletedTask;
        }

        private void EnsureUnique(Member member, Guid? excludedId)
        {
            if (Members.Any(existing => existing.Id != excludedId
                && (existing.StudentId == member.StudentId || existing.ClubEmail == member.ClubEmail)))
            {
                throw new MemberConflictException("Member StudentId or ClubEmail already exists.");
            }
        }
    }
}
