using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Tests;

public sealed class ProbationCandidateManagementTests
{
    [Fact]
    public async Task CreateWithValidCandidateWritesAudit()
    {
        var fixture = CreateFixture();
        var result = await fixture.Service.CreateAsync(
            fixture.Admin.Id,
            new CreateProbationCandidateCommand("candidate-002", "New Candidate", fixture.Department.Id, fixture.Generation.Id),
            "candidate-create",
            CancellationToken.None);

        Assert.Equal("CANDIDATE-002", result.StudentId);
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "ProbationCandidateCreated");
    }

    [Fact]
    public async Task UpdateLinkedCandidateUpdatesLinkedStudentIdAndWritesAudit()
    {
        var fixture = CreateFixture();
        fixture.Store.IdentityLinks.Add(DiscordIdentityLink.Create(123456789, fixture.Candidate.StudentId, DiscordIdentitySubjectType.Probation, fixture.Candidate.Id));

        var result = await fixture.Service.UpdateAsync(
            fixture.Admin.Id,
            fixture.Candidate.Id,
            new UpdateProbationCandidateCommand("candidate-009", "Renamed Candidate", fixture.Department.Id, fixture.Generation.Id),
            "candidate-update",
            CancellationToken.None);

        Assert.Equal("CANDIDATE-009", result.StudentId);
        Assert.Equal("CANDIDATE-009", Assert.Single(fixture.Store.IdentityLinks).StudentId);
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "ProbationCandidateUpdated");
    }

    [Fact]
    public async Task ChangeTeamMovesCandidateDirectlyAndQueuesRoleSync()
    {
        var fixture = CreateFixture();
        fixture.Candidate.AssignToTeam(fixture.Team.Id);
        var secondTeam = ProbationTeam.Create("Team Beta");
        fixture.Store.Teams.Add(secondTeam);
        fixture.Store.IdentityLinks.Add(DiscordIdentityLink.Create(123456789, fixture.Candidate.StudentId, DiscordIdentitySubjectType.Probation, fixture.Candidate.Id));

        var result = await fixture.Service.ChangeTeamAsync(
            fixture.Admin.Id,
            fixture.Candidate.Id,
            secondTeam.Id,
            "candidate-team-change",
            CancellationToken.None);

        Assert.Equal(secondTeam.Id, result.Team?.Id);
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "ProbationCandidateTeamChanged");
        Assert.Equal(DiscordSyncOperation.SynchronizeRoles, Assert.Single(fixture.Store.SyncJobs).Operation);
    }

    [Fact]
    public async Task CreateWhenStudentIdBelongsToMemberRejectsConflict()
    {
        var fixture = CreateFixture();
        fixture.MemberStore.Members.Add(Member.Create(
            "MEMBER001", "Existing Member", "member@example.org", fixture.Department.Id, false, fixture.Generation.Id, MemberPosition.Member));
        fixture.Store.TakenStudentIds.Add("MEMBER001");

        await Assert.ThrowsAsync<ProbationCandidateConflictException>(() => fixture.Service.CreateAsync(
            fixture.Admin.Id,
            new CreateProbationCandidateCommand("member001", "Duplicate Candidate", fixture.Department.Id, fixture.Generation.Id),
            "candidate-conflict",
            CancellationToken.None));
    }

    [Fact]
    public async Task MemberCannotManageCandidates()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ProbationCandidateManagementAccessDeniedException>(() => fixture.Service.ListAsync(
            fixture.RegularMember.Id,
            new ListProbationCandidatesQuery(),
            CancellationToken.None));
    }

    private static Fixture CreateFixture()
    {
        var coreDepartment = Department.CreateCore();
        var department = Department.CreateRegular("Technical", "technical");
        var generation = Generation.Create("Generation 1", "G1");
        var admin = Member.Create("ADMIN001", "Admin", "admin@example.org", coreDepartment.Id, true, generation.Id, MemberPosition.Admin);
        var regularMember = Member.Create("MEMBER002", "Regular Member", "regular@example.org", department.Id, false, generation.Id, MemberPosition.Member);
        var candidate = ProbationCandidate.Create("CANDIDATE001", "Candidate", department.Id, generation.Id);
        var team = ProbationTeam.Create("Team Alpha");
        var memberStore = new FakeMemberStore(coreDepartment, department, generation, admin, regularMember);
        var store = new FakeCandidateStore(department, generation, candidate, team);
        return new Fixture(new ProbationCandidateManagementService(store, memberStore), memberStore, store, admin, regularMember, candidate, department, generation, team);
    }

    private sealed record Fixture(
        ProbationCandidateManagementService Service,
        FakeMemberStore MemberStore,
        FakeCandidateStore Store,
        Member Admin,
        Member RegularMember,
        ProbationCandidate Candidate,
        Department Department,
        Generation Generation,
        ProbationTeam Team);

    private sealed class FakeCandidateStore(Department department, Generation generation, ProbationCandidate candidate, ProbationTeam team) : IProbationCandidateStore
    {
        public List<ProbationCandidate> Candidates { get; } = [candidate];
        public List<ProbationTeam> Teams { get; } = [team];
        public List<DiscordIdentityLink> IdentityLinks { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public List<DiscordSyncJob> SyncJobs { get; } = [];
        public HashSet<string> TakenStudentIds { get; } = new(StringComparer.Ordinal);

        public Task<(IReadOnlyList<ProbationCandidateView> Items, int TotalCount)> ListAsync(ListProbationCandidatesQuery query, CancellationToken cancellationToken)
        {
            var items = Candidates.Select(ToView).ToArray();
            return Task.FromResult<(IReadOnlyList<ProbationCandidateView>, int)>((items, items.Length));
        }

        public Task<ProbationCandidateView?> FindViewAsync(Guid candidateId, CancellationToken cancellationToken)
        {
            var item = Candidates.SingleOrDefault(candidate => candidate.Id == candidateId);
            return Task.FromResult(item is null ? null : (ProbationCandidateView?)ToView(item));
        }

        public Task<ProbationCandidate?> FindCandidateAsync(Guid candidateId, bool track, CancellationToken cancellationToken) => Task.FromResult(Candidates.SingleOrDefault(candidate => candidate.Id == candidateId));
        public Task<DiscordIdentityLink?> FindIdentityLinkAsync(Guid candidateId, bool track, CancellationToken cancellationToken) => Task.FromResult(IdentityLinks.SingleOrDefault(link => link.SubjectId == candidateId));
        public Task<bool> IsStudentIdTakenAsync(string studentId, Guid? excludedCandidateId, CancellationToken cancellationToken) => Task.FromResult(TakenStudentIds.Contains(studentId) || Candidates.Any(candidate => candidate.Id != excludedCandidateId && candidate.StudentId == studentId));
        public Task<ProbationTeam?> FindTeamAsync(Guid teamId, bool track, CancellationToken cancellationToken) => Task.FromResult(Teams.SingleOrDefault(team => team.Id == teamId));
        public Task<IReadOnlyList<ProbationCandidateTeamReference>> ListTeamsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProbationCandidateTeamReference>>(Teams.Select(team => new ProbationCandidateTeamReference(team.Id, team.Name, team.IsActive)).ToArray());
        public Task<IReadOnlyList<ProbationMentorOption>> SearchActiveMentorsAsync(string? search, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProbationMentorOption>>([]);

        public Task AddAsync(ProbationCandidate candidate, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Candidates.Add(candidate);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveUpdateAsync(ProbationCandidate candidate, DiscordIdentityLink? identityLink, AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveTeamChangeAsync(ProbationCandidate candidate, AuditLog auditLog, DiscordSyncJob? syncJob, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            if (syncJob is not null)
            {
                SyncJobs.Add(syncJob);
            }
            return Task.CompletedTask;
        }

        private ProbationCandidateView ToView(ProbationCandidate item)
        {
            var assignedTeam = Teams.SingleOrDefault(team => team.Id == item.TeamId);
            return new ProbationCandidateView(
                item,
                new ProbationCandidateReference(department.Id, department.Name, department.Slug),
                new ProbationCandidateReference(generation.Id, generation.Name, generation.Code),
                assignedTeam is null ? null : new ProbationCandidateTeamReference(assignedTeam.Id, assignedTeam.Name, assignedTeam.IsActive),
                IdentityLinks.Any(link => link.SubjectId == item.Id));
        }
    }

    private sealed class FakeMemberStore(Department coreDepartment, Department department, Generation generation, Member admin, Member regularMember) : IMemberStore
    {
        public List<Member> Members { get; } = [admin, regularMember];
        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken) => Task.FromResult(Members.SingleOrDefault(member => member.Id == memberId));
        public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(int page, int pageSize, MemberStatus? status, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(IReadOnlyCollection<string> studentIds, IReadOnlyCollection<string> clubEmails, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken) => Task.FromResult<Department?>(departmentId == coreDepartment.Id ? coreDepartment : departmentId == department.Id ? department : null);
        public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken) => Task.FromResult<Generation?>(generationId == generation.Id ? generation : null);
        public Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Department>>([coreDepartment, department]);
        public Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Generation>>([generation]);
        public Task AddAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddRangeAsync(IReadOnlyCollection<MemberAuditEntry> entries, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
