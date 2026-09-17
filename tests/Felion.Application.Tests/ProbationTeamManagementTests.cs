using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Tests;

public sealed class ProbationTeamManagementTests
{
    [Fact]
    public async Task CreateTeamWritesAuditAndReturnsActiveTeam()
    {
        var fixture = CreateFixture();
        var service = new ProbationTeamManagementService(fixture.TeamStore, fixture.MemberStore, fixture.RoleSynchronizationService);

        var result = await service.CreateAsync(
            fixture.Admin.Id,
            new CreateProbationTeamCommand(" Team Alpha "),
            "correlation-team-create",
            CancellationToken.None);

        Assert.Equal("Team Alpha", result.Name);
        Assert.True(result.IsActive);
        Assert.Contains(fixture.TeamStore.AuditLogs, audit => audit.Action == "ProbationTeamCreated");
    }

    [Fact]
    public async Task DiscordTeamMutationWritesDiscordActorAudit()
    {
        var fixture = CreateFixture();
        var service = new ProbationTeamManagementService(fixture.TeamStore, fixture.MemberStore);

        await service.CreateAsync(
            fixture.Admin.Id,
            new CreateProbationTeamCommand("Team Discord"),
            "discord-component-test",
            CancellationToken.None,
            actorDiscordUserId: 123456789);

        var audit = Assert.Single(fixture.TeamStore.AuditLogs);
        Assert.Equal(AuditActorType.DiscordMember, audit.ActorType);
        Assert.Null(audit.ActorMemberId);
        Assert.Equal(123456789, audit.ActorDiscordUserId);
    }

    [Fact]
    public async Task UpdateTeamSupportsSoftDeactivationAndWritesAudit()
    {
        var fixture = CreateFixture();
        var service = new ProbationTeamManagementService(fixture.TeamStore, fixture.MemberStore, fixture.RoleSynchronizationService);

        var result = await service.UpdateAsync(
            fixture.Admin.Id,
            fixture.Team.Id,
            new UpdateProbationTeamCommand("Team Renamed", IsActive: false),
            "correlation-team-update",
            CancellationToken.None);

        Assert.Equal("Team Renamed", result.Name);
        Assert.False(result.IsActive);
        Assert.Contains(fixture.TeamStore.AuditLogs, audit => audit.Action == "ProbationTeamUpdated");
    }

    [Fact]
    public async Task MemberCannotManageProbationTeams()
    {
        var fixture = CreateFixture();
        var service = new ProbationTeamManagementService(fixture.TeamStore, fixture.MemberStore, fixture.RoleSynchronizationService);

        await Assert.ThrowsAsync<ProbationTeamAccessDeniedException>(() => service.ListAsync(
            fixture.RegularMember.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task AssignCandidateWritesAuditAndSynchronizesImmediatelyWhenLinked()
    {
        var fixture = CreateFixture();
        Assert.Null(fixture.Candidate.TeamId);
        fixture.TeamStore.LinkedDiscordUsers[fixture.Candidate.Id] = 123456789;
        var service = new ProbationTeamManagementService(fixture.TeamStore, fixture.MemberStore, fixture.RoleSynchronizationService);

        var result = await service.AssignCandidateAsync(
            fixture.Admin.Id,
            fixture.Team.Id,
            fixture.Candidate.Id,
            "correlation-candidate-assign",
            CancellationToken.None);

        Assert.Contains(fixture.Candidate.Id, result.CandidateIds);
        Assert.Contains(fixture.TeamStore.AuditLogs, audit =>
            audit.Action == "ProbationCandidateAssignedToTeam"
            && audit.EntityId == fixture.Candidate.Id);
        Assert.Contains(
            (DiscordIdentitySubjectType.Probation, fixture.Candidate.Id),
            fixture.RoleSynchronizationService.SynchronizedSubjects);
    }

    [Fact]
    public async Task CandidateCannotBeAssignedToTwoTeams()
    {
        var fixture = CreateFixture();
        var otherTeam = ProbationTeam.Create("Team Beta");
        fixture.TeamStore.Teams.Add(otherTeam);
        fixture.Candidate.AssignToTeam(fixture.Team.Id);
        var service = new ProbationTeamManagementService(fixture.TeamStore, fixture.MemberStore, fixture.RoleSynchronizationService);

        await Assert.ThrowsAsync<ProbationTeamConflictException>(() => service.AssignCandidateAsync(
            fixture.Admin.Id,
            otherTeam.Id,
            fixture.Candidate.Id,
            "correlation-candidate-conflict",
            CancellationToken.None));
    }

    [Fact]
    public async Task RemoveCandidateWritesAuditAndSynchronizesImmediately()
    {
        var fixture = CreateFixture();
        fixture.Candidate.AssignToTeam(fixture.Team.Id);
        fixture.TeamStore.LinkedDiscordUsers[fixture.Candidate.Id] = 123456789;
        var service = new ProbationTeamManagementService(fixture.TeamStore, fixture.MemberStore, fixture.RoleSynchronizationService);

        var result = await service.RemoveCandidateAsync(
            fixture.Admin.Id,
            fixture.Team.Id,
            fixture.Candidate.Id,
            "correlation-candidate-remove",
            CancellationToken.None);

        Assert.DoesNotContain(fixture.Candidate.Id, result.CandidateIds);
        Assert.Contains(fixture.TeamStore.AuditLogs, audit => audit.Action == "ProbationCandidateRemovedFromTeam");
        Assert.Contains(
            (DiscordIdentitySubjectType.Probation, fixture.Candidate.Id),
            fixture.RoleSynchronizationService.SynchronizedSubjects);
    }

    [Fact]
    public async Task InactiveMemberCannotBeAssignedAsMentor()
    {
        var fixture = CreateFixture();
        fixture.RegularMember.Deactivate();
        var service = new ProbationTeamManagementService(fixture.TeamStore, fixture.MemberStore, fixture.RoleSynchronizationService);

        await Assert.ThrowsAsync<ProbationTeamValidationException>(() => service.AssignMentorAsync(
            fixture.Admin.Id,
            fixture.Team.Id,
            fixture.RegularMember.Id,
            "correlation-mentor-invalid",
            CancellationToken.None));
    }

    [Fact]
    public async Task OneActiveMentorCanBeAssignedToMultipleTeams()
    {
        var fixture = CreateFixture();
        var otherTeam = ProbationTeam.Create("Team Beta");
        fixture.TeamStore.Teams.Add(otherTeam);
        var service = new ProbationTeamManagementService(fixture.TeamStore, fixture.MemberStore, fixture.RoleSynchronizationService);

        await service.AssignMentorAsync(
            fixture.Admin.Id,
            fixture.Team.Id,
            fixture.RegularMember.Id,
            "correlation-mentor-one",
            CancellationToken.None);
        var secondResult = await service.AssignMentorAsync(
            fixture.Admin.Id,
            otherTeam.Id,
            fixture.RegularMember.Id,
            "correlation-mentor-two",
            CancellationToken.None);

        Assert.Contains(fixture.RegularMember.Id, secondResult.MentorMemberIds);
        Assert.Equal(2, fixture.TeamStore.Mentors.Count);
    }

    private static Fixture CreateFixture()
    {
        var memberStore = new FakeMemberStore
        {
            CoreDepartment = Department.CreateCore(),
            RegularDepartment = Department.CreateRegular("Technical", "technical"),
            Generation = Generation.Create("Generation 1", "G1")
        };
        var admin = Member.Create(
            "ADMIN001",
            "Admin",
            "admin@example.org",
            memberStore.CoreDepartment.Id,
            isCoreDepartment: true,
            memberStore.Generation.Id,
            MemberPosition.Admin);
        var regularMember = Member.Create(
            "MEMBER001",
            "Mentor",
            "mentor@example.org",
            memberStore.RegularDepartment.Id,
            isCoreDepartment: false,
            memberStore.Generation.Id,
            MemberPosition.Member);
        memberStore.Members.Add(admin);
        memberStore.Members.Add(regularMember);

        var team = ProbationTeam.Create("Team Alpha");
        var candidate = ProbationCandidate.Create(
            "CANDIDATE001",
            "Candidate",
            memberStore.RegularDepartment.Id,
            memberStore.Generation.Id);
        var teamStore = new FakeTeamStore();
        teamStore.Teams.Add(team);
        teamStore.Candidates.Add(candidate);
        return new Fixture(
            memberStore,
            teamStore,
            admin,
            regularMember,
            team,
            candidate,
            new TestDiscordRoleSynchronizationService());
    }

    private sealed record Fixture(
        FakeMemberStore MemberStore,
        FakeTeamStore TeamStore,
        Member Admin,
        Member RegularMember,
        ProbationTeam Team,
        ProbationCandidate Candidate,
        TestDiscordRoleSynchronizationService RoleSynchronizationService);

    private sealed class FakeTeamStore : IProbationTeamStore
    {
        public List<ProbationTeam> Teams { get; } = [];

        public List<ProbationCandidate> Candidates { get; } = [];

        public List<TeamMentor> Mentors { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public Dictionary<Guid, long> LinkedDiscordUsers { get; } = [];

        public Task<IReadOnlyList<ProbationTeamView>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ProbationTeamView>>(Teams.Select(ToView).ToArray());
        }

        public Task<ProbationTeamView?> FindViewAsync(Guid teamId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Teams.SingleOrDefault(team => team.Id == teamId) is { } team
                ? (ProbationTeamView?)ToView(team)
                : null);
        }

        public Task<ProbationTeam?> FindTeamAsync(Guid teamId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Teams.SingleOrDefault(team => team.Id == teamId));
        }

        public Task<ProbationCandidate?> FindCandidateAsync(Guid candidateId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Candidates.SingleOrDefault(candidate => candidate.Id == candidateId));
        }

        public Task<TeamMentor?> FindMentorAsync(Guid teamId, Guid memberId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Mentors.SingleOrDefault(mentor => mentor.TeamId == teamId && mentor.MemberId == memberId));
        }

        public Task<long?> FindCandidateDiscordUserIdAsync(Guid candidateId, CancellationToken cancellationToken)
        {
            return Task.FromResult(LinkedDiscordUsers.TryGetValue(candidateId, out var discordUserId)
                ? (long?)discordUserId
                : null);
        }

        public Task AddTeamAsync(ProbationTeam team, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Teams.Add(team);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task UpdateTeamAsync(ProbationTeam team, AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveCandidateAssignmentAsync(
            ProbationCandidate candidate,
            AuditLog auditLog,
            CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddMentorAsync(TeamMentor mentor, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Mentors.Add(mentor);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task RemoveMentorAsync(TeamMentor mentor, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Mentors.Remove(mentor);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        private ProbationTeamView ToView(ProbationTeam team)
        {
            return new ProbationTeamView(
                team.Id,
                team.Name,
                team.IsActive,
                Candidates.Where(candidate => candidate.TeamId == team.Id).Select(candidate => candidate.Id).ToArray(),
                Mentors.Where(mentor => mentor.TeamId == team.Id).Select(mentor => mentor.MemberId).ToArray(),
                team.CreatedAt,
                team.UpdatedAt);
        }
    }

    private sealed class FakeMemberStore : IMemberStore
    {
        public List<Member> Members { get; } = [];

        public required Department CoreDepartment { get; init; }

        public required Department RegularDepartment { get; init; }

        public required Generation Generation { get; init; }

        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Members.SingleOrDefault(member => member.Id == memberId));
        }

        public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(int page, int pageSize, MemberStatus? status, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(IReadOnlyCollection<string> studentIds, IReadOnlyCollection<string> clubEmails, CancellationToken cancellationToken)
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
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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
}
