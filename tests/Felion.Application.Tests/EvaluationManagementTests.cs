using Felion.Application.Discord;
using Felion.Application.Hardening;
using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Evaluation;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Tests;

public sealed class EvaluationManagementTests
{
    [Fact]
    public async Task AdminCreatesOpenPeriodAndOnlyAdminCanCloseIt()
    {
        var fixture = CreateFixture();

        var created = await fixture.Service.CreatePeriodAsync(
            fixture.Admin.Id,
            new CreateEvaluationPeriodCommand(" Week 1 "),
            "create",
            9001,
            CancellationToken.None);

        Assert.Equal("Week 1", created.Name);
        Assert.Equal(EvaluationPeriodStatus.Open, created.Status);
        Assert.Contains(fixture.Store.Audits, audit => audit.Action == "EvaluationPeriodCreated");
        await Assert.ThrowsAsync<EvaluationAdminAccessDeniedException>(() =>
            fixture.Service.ClosePeriodAsync(fixture.Core.Id, created.Id, "close", 9002, CancellationToken.None));

        var closed = await fixture.Service.ClosePeriodAsync(
            fixture.Admin.Id,
            created.Id,
            "close",
            9001,
            CancellationToken.None);

        Assert.Equal(EvaluationPeriodStatus.Closed, closed.Status);
        Assert.Contains(fixture.Store.Audits, audit => audit.Action == "EvaluationPeriodClosed");
    }

    [Fact]
    public async Task NamedQueriesResolvePeriodAndActiveTeamCaseInsensitively()
    {
        var fixture = CreateFixture();
        var period = fixture.AddOpenPeriod();

        var summary = await fixture.Service.SummaryByNameAsync(
            fixture.Admin.Id,
            " week 1 ",
            " team alpha ",
            CancellationToken.None);
        var detail = await fixture.Service.ViewByPeriodNameAsync(
            fixture.Core.Id,
            fixture.Target.Id,
            "WEEK 1",
            CancellationToken.None);

        Assert.Equal(period.Id, summary.Period.Id);
        Assert.Equal("Team Alpha", summary.TeamName);
        var targetSummary = summary.Candidates.Single(candidate => candidate.CandidateId == fixture.Target.Id);
        Assert.Equal(fixture.Team.Id, targetSummary.TeamId);
        Assert.Equal(period.Id, detail.Period.Id);
    }

    [Fact]
    public async Task NamedPeriodQueryRejectsAmbiguousNames()
    {
        var fixture = CreateFixture();
        fixture.AddOpenPeriod();
        fixture.AddOpenPeriod();

        await Assert.ThrowsAsync<EvaluationPeriodNameAmbiguousException>(() =>
            fixture.Service.GetStatusByNameAsync(
                fixture.Admin.Id,
                "WEEK 1",
                CancellationToken.None));
    }

    [Fact]
    public async Task PeerSubmissionCanBeUpdatedButCannotCrossTeamOrSelfReview()
    {
        var fixture = CreateFixture();
        var period = fixture.AddOpenPeriod();

        var first = await fixture.Service.SubmitPeerEvaluationAsync(
            fixture.PeerDiscordId,
            new SubmitPeerEvaluationCommand(period.Id, fixture.Target.Id, 4, 5, 3, "Helpful"),
            "peer-create",
            CancellationToken.None);
        var second = await fixture.Service.SubmitPeerEvaluationAsync(
            fixture.PeerDiscordId,
            new SubmitPeerEvaluationCommand(period.Id, fixture.Target.Id, 5, 5, 4),
            "peer-update",
            CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.True(second.Updated);
        Assert.Equal(5, Assert.Single(fixture.Store.PeerEvaluations).Contribution);
        Assert.Contains(fixture.Store.Audits, audit => audit.Action == "PeerEvaluationSubmitted");
        Assert.Contains(fixture.Store.Audits, audit => audit.Action == "PeerEvaluationUpdated");

        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() =>
            fixture.Service.SubmitPeerEvaluationAsync(
                fixture.PeerDiscordId,
                new SubmitPeerEvaluationCommand(period.Id, fixture.Reviewer.Id, 4, 4, 4),
                "self",
                CancellationToken.None));
        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() =>
            fixture.Service.SubmitPeerEvaluationAsync(
                fixture.PeerDiscordId,
                new SubmitPeerEvaluationCommand(period.Id, fixture.OtherTeamCandidate.Id, 4, 4, 4),
                "other-team",
                CancellationToken.None));
    }

    [Fact]
    public async Task PeerScoresOutsideOneToFiveAreRejected()
    {
        var fixture = CreateFixture();
        var period = fixture.AddOpenPeriod();

        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() =>
            fixture.Service.SubmitPeerEvaluationAsync(
                fixture.PeerDiscordId,
                new SubmitPeerEvaluationCommand(period.Id, fixture.Target.Id, 0, 4, 4),
                "invalid",
                CancellationToken.None));
        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() =>
            fixture.Service.SubmitPeerEvaluationAsync(
                fixture.PeerDiscordId,
                new SubmitPeerEvaluationCommand(period.Id, fixture.Target.Id, 6, 4, 4),
                "invalid",
                CancellationToken.None));
    }

    [Fact]
    public async Task ClosedPeriodRejectsNewPeerSubmission()
    {
        var fixture = CreateFixture();
        var period = fixture.AddOpenPeriod();
        period.Close();

        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() =>
            fixture.Service.SubmitPeerEvaluationAsync(
                fixture.PeerDiscordId,
                new SubmitPeerEvaluationCommand(period.Id, fixture.Target.Id, 4, 4, 4),
                "closed",
                CancellationToken.None));
    }

    [Fact]
    public async Task MultipleMentorsMayEvaluateOneCandidateButEachMentorIsUnique()
    {
        var fixture = CreateFixture();
        var period = fixture.AddOpenPeriod();

        var first = await fixture.Service.SubmitMentorEvaluationAsync(
            fixture.MentorDiscordId,
            new SubmitMentorEvaluationCommand(period.Id, fixture.Target.Id, 9, 8, 10),
            "mentor-create",
            CancellationToken.None);
        var second = await fixture.Service.SubmitMentorEvaluationAsync(
            fixture.MentorDiscordId,
            new SubmitMentorEvaluationCommand(period.Id, fixture.Target.Id, 10, 9, 10),
            "mentor-update",
            CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.True(second.Updated);
        Assert.Contains(fixture.Store.Audits, audit => audit.Action == "MentorEvaluationSubmitted");
        Assert.Contains(fixture.Store.Audits, audit => audit.Action == "MentorEvaluationUpdated");

        fixture.AddSecondMentor();
        var otherMentor = await fixture.Service.SubmitMentorEvaluationAsync(
            fixture.SecondMentorDiscordId,
            new SubmitMentorEvaluationCommand(period.Id, fixture.Target.Id, 8, 8, 8),
            "mentor-two",
            CancellationToken.None);

        Assert.NotEqual(first.Id, otherMentor.Id);
        Assert.Equal(2, fixture.Store.MentorEvaluations.Count);
    }

    [Fact]
    public async Task MentorOutsideTenPointRangeOrTeamIsRejected()
    {
        var fixture = CreateFixture();
        var period = fixture.AddOpenPeriod();

        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() =>
            fixture.Service.SubmitMentorEvaluationAsync(
                fixture.MentorDiscordId,
                new SubmitMentorEvaluationCommand(period.Id, fixture.Target.Id, 11, 5, 5),
                "invalid",
                CancellationToken.None));
        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() =>
            fixture.Service.SubmitMentorEvaluationAsync(
                fixture.MentorDiscordId,
                new SubmitMentorEvaluationCommand(period.Id, fixture.OtherTeamCandidate.Id, 5, 5, 5),
                "other-team",
                CancellationToken.None));
    }

    [Fact]
    public async Task SummaryCalculatesSeparatePeerAndMentorAveragesAndProgress()
    {
        var fixture = CreateFixture();
        var period = fixture.AddOpenPeriod();
        await fixture.Service.SubmitPeerEvaluationAsync(
            fixture.PeerDiscordId,
            new SubmitPeerEvaluationCommand(period.Id, fixture.Target.Id, 4, 5, 3),
            "peer",
            CancellationToken.None);
        await fixture.Service.SubmitMentorEvaluationAsync(
            fixture.MentorDiscordId,
            new SubmitMentorEvaluationCommand(period.Id, fixture.Target.Id, 9, 8, 10),
            "mentor",
            CancellationToken.None);

        var summary = await fixture.Service.SummaryAsync(
            fixture.Admin.Id,
            period.Id,
            fixture.Team.Id,
            CancellationToken.None);
        var target = Assert.Single(summary.Candidates, candidate => candidate.CandidateId == fixture.Target.Id);

        Assert.Equal(4, target.Peer.AverageContribution);
        Assert.Equal(1, target.Peer.EvaluationCount);
        Assert.Equal(9, target.Mentor.AverageAttendance);
        Assert.Equal(1, target.Mentor.EvaluationCount);
        Assert.Equal(1, summary.Progress.PeerSubmitted);
        Assert.Equal(2, summary.Progress.PeerExpected);
        Assert.Equal(1, summary.Progress.MentorSubmitted);
        Assert.Equal(2, summary.Progress.MentorExpected);
        Assert.Single(summary.Progress.MissingPeerEvaluations);
        Assert.Single(summary.Progress.MissingMentorEvaluations);
    }

    [Fact]
    public async Task CandidateCannotReadEvaluationDetails()
    {
        var fixture = CreateFixture();
        var period = fixture.AddOpenPeriod();

        await Assert.ThrowsAsync<EvaluationAccessDeniedException>(() =>
            fixture.Service.ViewAsync(fixture.RegularMember.Id, fixture.Target.Id, period.Id, CancellationToken.None));
    }

    private static Fixture CreateFixture()
    {
        var memberStore = new FakeMemberStore();
        var coreDepartment = Department.CreateCore();
        var regularDepartment = Department.CreateRegular("Technical", "technical");
        var generation = Generation.Create("Generation 1", "G1");
        var admin = Member.Create("ADMIN001", "Admin", "admin@example.org", coreDepartment.Id, true, generation.Id, MemberPosition.Admin);
        var core = Member.Create("CORE001", "Core", "core@example.org", coreDepartment.Id, true, generation.Id, MemberPosition.Core);
        var mentor = Member.Create("MENTOR001", "Mentor", "mentor@example.org", regularDepartment.Id, false, generation.Id, MemberPosition.Member);
        var regular = Member.Create("MEMBER001", "Member", "member@example.org", regularDepartment.Id, false, generation.Id, MemberPosition.Member);
        memberStore.Members.AddRange([admin, core, mentor, regular]);

        var team = ProbationTeam.Create("Team Alpha");
        var otherTeam = ProbationTeam.Create("Team Beta");
        var reviewer = ProbationCandidate.Create("P001", "Reviewer", regularDepartment.Id, generation.Id, team.Id);
        var target = ProbationCandidate.Create("P002", "Target", regularDepartment.Id, generation.Id, team.Id);
        var other = ProbationCandidate.Create("P003", "Other", regularDepartment.Id, generation.Id, otherTeam.Id);
        var teamStore = new FakeTeamStore();
        teamStore.Teams.AddRange([team, otherTeam]);
        teamStore.Candidates.AddRange([reviewer, target, other]);
        teamStore.Mentors.Add(TeamMentor.Create(team.Id, mentor.Id));

        var links = new FakeDiscordLinkStore();
        links.Links.Add(DiscordIdentityLink.Create(1001, reviewer.StudentId, DiscordIdentitySubjectType.Probation, reviewer.Id));
        links.Links.Add(DiscordIdentityLink.Create(1002, mentor.StudentId, DiscordIdentitySubjectType.Member, mentor.Id));

        var fixture = new Fixture(
            new FakeEvaluationStore(),
            memberStore,
            teamStore,
            new FakeCandidateStore(teamStore, regularDepartment, generation),
            links,
            admin,
            core,
            mentor,
            regular,
            team,
            reviewer,
            target,
            other);
        fixture.Service = new EvaluationManagementService(
            fixture.Store,
            fixture.MemberStore,
            fixture.TeamStore,
            fixture.CandidateStore,
            fixture.LinkStore,
            new AllowRateLimitGate());
        return fixture;
    }

    private sealed class Fixture(
        FakeEvaluationStore store,
        FakeMemberStore memberStore,
        FakeTeamStore teamStore,
        FakeCandidateStore candidateStore,
        FakeDiscordLinkStore linkStore,
        Member admin,
        Member core,
        Member mentor,
        Member regularMember,
        ProbationTeam team,
        ProbationCandidate reviewer,
        ProbationCandidate target,
        ProbationCandidate otherTeamCandidate)
    {
        public FakeEvaluationStore Store { get; } = store;
        public FakeMemberStore MemberStore { get; } = memberStore;
        public FakeTeamStore TeamStore { get; } = teamStore;
        public FakeCandidateStore CandidateStore { get; } = candidateStore;
        public FakeDiscordLinkStore LinkStore { get; } = linkStore;
        public Member Admin { get; } = admin;
        public Member Core { get; } = core;
        public Member Mentor { get; } = mentor;
        public Member RegularMember { get; } = regularMember;
        public ProbationTeam Team { get; } = team;
        public ProbationCandidate Reviewer { get; } = reviewer;
        public ProbationCandidate Target { get; } = target;
        public ProbationCandidate OtherTeamCandidate { get; } = otherTeamCandidate;
        public long PeerDiscordId => LinkStore.Links.Single(link => link.SubjectId == Reviewer.Id).DiscordUserId;
        public long MentorDiscordId => LinkStore.Links.Single(link => link.SubjectId == Mentor.Id).DiscordUserId;
        public long SecondMentorDiscordId => LinkStore.Links.First().DiscordUserId + 2;
        public EvaluationManagementService Service { get; set; } = null!;

        public EvaluationPeriod AddOpenPeriod()
        {
            var period = EvaluationPeriod.Create("Week 1");
            Store.Periods.Add(period);
            return period;
        }

        public void AddSecondMentor()
        {
            var second = Member.Create(
                "MENTOR002",
                "Mentor Two",
                "mentor2@example.org",
                RegularMember.DepartmentId,
                false,
                RegularMember.GenerationId,
                MemberPosition.Member);
            MemberStore.Members.Add(second);
            TeamStore.Mentors.Add(TeamMentor.Create(Team.Id, second.Id));
            LinkStore.Links.Add(DiscordIdentityLink.Create(SecondMentorDiscordId, second.StudentId, DiscordIdentitySubjectType.Member, second.Id));
        }
    }

    private sealed class AllowRateLimitGate : IRateLimitGate
    {
        public ValueTask<RateLimitDecision> TryAcquireAsync(
            RateLimitOperation operation,
            string partitionKey,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new RateLimitDecision(true, null));
    }

    private sealed class FakeEvaluationStore : IEvaluationStore
    {
        public List<EvaluationPeriod> Periods { get; } = [];
        public List<PeerEvaluation> PeerEvaluations { get; } = [];
        public List<MentorEvaluation> MentorEvaluations { get; } = [];
        public List<AuditLog> Audits { get; } = [];

        public Task<IReadOnlyList<EvaluationPeriod>> ListPeriodsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EvaluationPeriod>>(Periods);

        public Task<EvaluationPeriod?> FindPeriodAsync(Guid periodId, bool track, CancellationToken cancellationToken) =>
            Task.FromResult(Periods.SingleOrDefault(period => period.Id == periodId));

        public Task<PeerEvaluation?> FindPeerEvaluationAsync(Guid periodId, Guid evaluatorCandidateId, Guid targetCandidateId, bool track, CancellationToken cancellationToken) =>
            Task.FromResult(PeerEvaluations.SingleOrDefault(evaluation =>
                evaluation.EvaluationPeriodId == periodId
                && evaluation.EvaluatorCandidateId == evaluatorCandidateId
                && evaluation.TargetCandidateId == targetCandidateId));

        public Task<MentorEvaluation?> FindMentorEvaluationAsync(Guid periodId, Guid mentorMemberId, Guid targetCandidateId, bool track, CancellationToken cancellationToken) =>
            Task.FromResult(MentorEvaluations.SingleOrDefault(evaluation =>
                evaluation.EvaluationPeriodId == periodId
                && evaluation.MentorMemberId == mentorMemberId
                && evaluation.TargetCandidateId == targetCandidateId));

        public Task<IReadOnlyList<PeerEvaluation>> ListPeerEvaluationsAsync(Guid periodId, Guid? targetCandidateId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PeerEvaluation>>(PeerEvaluations
                .Where(evaluation => evaluation.EvaluationPeriodId == periodId && (targetCandidateId is null || evaluation.TargetCandidateId == targetCandidateId))
                .ToArray());

        public Task<IReadOnlyList<MentorEvaluation>> ListMentorEvaluationsAsync(Guid periodId, Guid? targetCandidateId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MentorEvaluation>>(MentorEvaluations
                .Where(evaluation => evaluation.EvaluationPeriodId == periodId && (targetCandidateId is null || evaluation.TargetCandidateId == targetCandidateId))
                .ToArray());

        public Task AddPeriodAsync(EvaluationPeriod period, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Periods.Add(period);
            Audits.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SavePeerEvaluationAsync(PeerEvaluation evaluation, AuditLog auditLog, bool isNew, CancellationToken cancellationToken)
        {
            if (isNew)
            {
                PeerEvaluations.Add(evaluation);
            }

            Audits.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveMentorEvaluationAsync(MentorEvaluation evaluation, AuditLog auditLog, bool isNew, CancellationToken cancellationToken)
        {
            if (isNew)
            {
                MentorEvaluations.Add(evaluation);
            }

            Audits.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task ClosePeriodAsync(EvaluationPeriod period, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Audits.Add(auditLog);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMemberStore : IMemberStore
    {
        public List<Member> Members { get; } = [];

        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken) =>
            Task.FromResult(Members.SingleOrDefault(member => member.Id == memberId));

        public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(int page, int pageSize, MemberStatus? status, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<Member>)Members, Members.Count));

        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(IReadOnlyCollection<string> studentIds, IReadOnlyCollection<string> clubEmails, CancellationToken cancellationToken) =>
            Task.FromResult(new MemberIdentityLookup(new HashSet<string>(), new HashSet<string>()));

        public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken) => Task.FromResult<Department?>(null);
        public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken) => Task.FromResult<Generation?>(null);
        public Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Department>>([]);
        public Task<IReadOnlyList<Generation>> ListGenerationsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Generation>>([]);
        public Task AddAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(Member member, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddRangeAsync(IReadOnlyCollection<MemberAuditEntry> entries, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeDiscordLinkStore : IDiscordLinkStore
    {
        public List<DiscordIdentityLink> Links { get; } = [];
        public Task<IReadOnlyList<DiscordLinkSubject>> FindEligibleSubjectsByStudentIdAsync(string normalizedStudentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DiscordLinkSubject>>([]);
        public Task<DiscordIdentityLink?> FindByStudentIdAsync(string normalizedStudentId, CancellationToken cancellationToken) => Task.FromResult<DiscordIdentityLink?>(null);
        public Task<DiscordIdentityLink?> FindByDiscordUserIdAsync(long discordUserId, CancellationToken cancellationToken) => Task.FromResult(Links.SingleOrDefault(link => link.DiscordUserId == discordUserId));
        public Task AddAsync(DiscordIdentityLink link, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTeamStore : IProbationTeamStore
    {
        public List<ProbationTeam> Teams { get; } = [];
        public List<ProbationCandidate> Candidates { get; } = [];
        public List<TeamMentor> Mentors { get; } = [];

        public Task<IReadOnlyList<ProbationTeamView>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProbationTeamView>>(Teams.Select(ToView).ToArray());

        public Task<ProbationTeamView?> FindViewAsync(Guid teamId, CancellationToken cancellationToken) =>
            Task.FromResult(Teams.SingleOrDefault(team => team.Id == teamId) is { } team ? (ProbationTeamView?)ToView(team) : null);

        public Task<ProbationTeam?> FindTeamAsync(Guid teamId, bool track, CancellationToken cancellationToken) =>
            Task.FromResult(Teams.SingleOrDefault(team => team.Id == teamId));

        public Task<ProbationCandidate?> FindCandidateAsync(Guid candidateId, bool track, CancellationToken cancellationToken) =>
            Task.FromResult(Candidates.SingleOrDefault(candidate => candidate.Id == candidateId));

        public Task<TeamMentor?> FindMentorAsync(Guid teamId, Guid memberId, bool track, CancellationToken cancellationToken) =>
            Task.FromResult(Mentors.SingleOrDefault(mentor => mentor.TeamId == teamId && mentor.MemberId == memberId));

        public Task<long?> FindCandidateDiscordUserIdAsync(Guid candidateId, CancellationToken cancellationToken) => Task.FromResult<long?>(null);
        public Task AddTeamAsync(ProbationTeam team, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateTeamAsync(ProbationTeam team, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveCandidateAssignmentAsync(ProbationCandidate candidate, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddMentorAsync(TeamMentor mentor, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveMentorAsync(TeamMentor mentor, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;

        private ProbationTeamView ToView(ProbationTeam team) =>
            new(
                team.Id,
                team.Name,
                team.IsActive,
                Candidates.Where(candidate => candidate.TeamId == team.Id).Select(candidate => candidate.Id).ToArray(),
                Mentors.Where(mentor => mentor.TeamId == team.Id).Select(mentor => mentor.MemberId).ToArray(),
                team.CreatedAt,
                team.UpdatedAt,
                Candidates.Where(candidate => candidate.TeamId == team.Id)
                    .Select(candidate => new ProbationTeamCandidateSummary(candidate.Id, candidate.StudentId, candidate.FullName))
                    .ToArray());
    }

    private sealed class FakeCandidateStore(FakeTeamStore teamStore, Department department, Generation generation) : IProbationCandidateStore
    {
        public Task<(IReadOnlyList<ProbationCandidateView> Items, int TotalCount)> ListAsync(ListProbationCandidatesQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<ProbationCandidateView>)[], 0));

        public Task<ProbationCandidateView?> FindViewAsync(Guid candidateId, CancellationToken cancellationToken)
        {
            var candidate = teamStore.Candidates.SingleOrDefault(item => item.Id == candidateId);
            return Task.FromResult(candidate is null ? null : (ProbationCandidateView?)new ProbationCandidateView(
                candidate,
                new ProbationCandidateReference(department.Id, department.Name, department.Slug),
                new ProbationCandidateReference(generation.Id, generation.Name, generation.Code),
                candidate.TeamId is { } teamId
                    ? new ProbationCandidateTeamReference(teamId, teamStore.Teams.Single(team => team.Id == teamId).Name, true)
                    : null,
                false));
        }

        public Task<ProbationCandidate?> FindCandidateAsync(Guid candidateId, bool track, CancellationToken cancellationToken) =>
            teamStore.FindCandidateAsync(candidateId, track, cancellationToken);
        public Task<DiscordIdentityLink?> FindIdentityLinkAsync(Guid candidateId, bool track, CancellationToken cancellationToken) => Task.FromResult<DiscordIdentityLink?>(null);
        public Task<bool> IsStudentIdTakenAsync(string studentId, Guid? excludedCandidateId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<ProbationTeam?> FindTeamAsync(Guid teamId, bool track, CancellationToken cancellationToken) => teamStore.FindTeamAsync(teamId, track, cancellationToken);
        public Task<IReadOnlyList<ProbationCandidateTeamReference>> ListTeamsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProbationCandidateTeamReference>>([]);
        public Task<IReadOnlyList<ProbationMentorOption>> SearchActiveMentorsAsync(string? search, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProbationMentorOption>>([]);
        public Task AddAsync(ProbationCandidate candidate, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveUpdateAsync(ProbationCandidate candidate, DiscordIdentityLink? identityLink, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveTeamChangeAsync(ProbationCandidate candidate, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordAuditAsync(AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
