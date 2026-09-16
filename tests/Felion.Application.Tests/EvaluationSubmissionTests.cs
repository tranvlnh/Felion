using Felion.Application.Hardening;
using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Evaluation;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Tests;

public sealed class EvaluationSubmissionTests
{
    [Fact]
    public async Task PeerCanEvaluateAnotherActiveCandidateInTheSameTeam()
    {
        var fixture = CreateFixture();
        fixture.Period.Open();
        var service = CreateService(fixture);

        var receipt = await service.SubmitPeerEvaluationAsync(
            fixture.PeerReviewer.Id,
            new SubmitEvaluationCommand(
                fixture.PeerForm.Id,
                fixture.Target.Id,
                [new EvaluationAnswerCommand(fixture.ScoreQuestion.Id, 4)]),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, receipt.Id);
        Assert.Single(fixture.EvaluationStore.Submissions);
        Assert.Equal(4, Assert.Single(fixture.EvaluationStore.Answers).ScoreValue);
    }

    [Fact]
    public async Task PeerCannotSelfReviewOrReviewAnotherTeam()
    {
        var fixture = CreateFixture();
        var service = CreateService(fixture);

        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() => service.SubmitPeerEvaluationAsync(
            fixture.PeerReviewer.Id,
            new SubmitEvaluationCommand(fixture.PeerForm.Id, fixture.PeerReviewer.Id, []),
            CancellationToken.None));
        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() => service.SubmitPeerEvaluationAsync(
            fixture.PeerReviewer.Id,
            new SubmitEvaluationCommand(fixture.PeerForm.Id, fixture.OtherTeamCandidate.Id, []),
            CancellationToken.None));
        Assert.Empty(fixture.EvaluationStore.Submissions);
    }

    [Fact]
    public async Task MentorMustBeAssignedToTargetTeam()
    {
        var fixture = CreateFixture();
        fixture.Period.Open();
        var service = CreateService(fixture);
        var command = new SubmitEvaluationCommand(
            fixture.MentorForm.Id,
            fixture.Target.Id,
            [new EvaluationAnswerCommand(fixture.TextQuestion.Id, TextValue: "Good progress")]);

        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() => service.SubmitMentorEvaluationAsync(
            fixture.Mentor.Id,
            command,
            CancellationToken.None));

        fixture.ProbationStore.Mentors.Add(TeamMentor.Create(fixture.Team.Id, fixture.Mentor.Id));
        var receipt = await service.SubmitMentorEvaluationAsync(
            fixture.Mentor.Id,
            command,
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, receipt.Id);
        Assert.Single(fixture.EvaluationStore.Submissions);
    }

    [Fact]
    public async Task DraftAndClosedPeriodsRejectSubmissions()
    {
        var fixture = CreateFixture();
        var service = CreateService(fixture);
        var command = new SubmitEvaluationCommand(fixture.PeerForm.Id, fixture.Target.Id, []);

        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() => service.SubmitPeerEvaluationAsync(
            fixture.PeerReviewer.Id,
            command,
            CancellationToken.None));

        fixture.Period.Open();
        fixture.Period.Close();
        await Assert.ThrowsAsync<EvaluationSubmissionValidationException>(() => service.SubmitPeerEvaluationAsync(
            fixture.PeerReviewer.Id,
            command,
            CancellationToken.None));
    }

    [Fact]
    public async Task RepeatedSubmissionEditsExistingSubmissionInsteadOfCreatingDuplicate()
    {
        var fixture = CreateFixture();
        fixture.Period.Open();
        var service = CreateService(fixture);
        var command = new SubmitEvaluationCommand(
            fixture.PeerForm.Id,
            fixture.Target.Id,
            [new EvaluationAnswerCommand(fixture.ScoreQuestion.Id, 3)]);

        var first = await service.SubmitPeerEvaluationAsync(
            fixture.PeerReviewer.Id,
            command,
            CancellationToken.None);
        var second = await service.SubmitPeerEvaluationAsync(
            fixture.PeerReviewer.Id,
            command with
            {
                Answers = [new EvaluationAnswerCommand(fixture.ScoreQuestion.Id, 5)]
            },
            CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(fixture.EvaluationStore.Submissions);
        Assert.Equal(5, Assert.Single(fixture.EvaluationStore.Answers).ScoreValue);
    }

    [Fact]
    public async Task ResultsAreRestrictedToCoreOrAdmin()
    {
        var fixture = CreateFixture();
        fixture.Period.Open();
        var service = CreateService(fixture);
        await service.SubmitPeerEvaluationAsync(
            fixture.PeerReviewer.Id,
            new SubmitEvaluationCommand(
                fixture.PeerForm.Id,
                fixture.Target.Id,
                [new EvaluationAnswerCommand(fixture.ScoreQuestion.Id, 4)]),
            CancellationToken.None);

        await Assert.ThrowsAsync<EvaluationAccessDeniedException>(() => service.ListResultsAsync(
            fixture.RegularMember.Id,
            null,
            null,
            CancellationToken.None));
        var results = await service.ListResultsAsync(fixture.Admin.Id, fixture.Period.Id, null, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(fixture.PeerReviewer.Id, result.ReviewerCandidateId);
        Assert.Equal(4, Assert.Single(result.Answers).ScoreValue);
    }

    [Fact]
    public async Task SubmissionRejectsRequestsWhenTheReviewerRateLimitIsReached()
    {
        var fixture = CreateFixture();
        var rateLimitGate = new TestRateLimitGate(isAcquired: false, retryAfter: TimeSpan.FromMinutes(1));
        var service = CreateService(fixture, rateLimitGate);

        var exception = await Assert.ThrowsAsync<RateLimitExceededException>(() => service.SubmitPeerEvaluationAsync(
            fixture.PeerReviewer.Id,
            new SubmitEvaluationCommand(fixture.PeerForm.Id, fixture.Target.Id, []),
            CancellationToken.None));

        Assert.Equal(RateLimitOperation.EvaluationSubmission, exception.Operation);
        Assert.Equal(TimeSpan.FromMinutes(1), exception.RetryAfter);
        Assert.Contains(rateLimitGate.Attempts, attempt => attempt.PartitionKey == $"Peer:{fixture.PeerReviewer.Id:N}");
    }

    private static EvaluationManagementService CreateService(
        Fixture fixture,
        TestRateLimitGate? rateLimitGate = null)
    {
        return new EvaluationManagementService(
            fixture.EvaluationStore,
            fixture.MemberStore,
            new DefaultsProvider(),
            fixture.ProbationStore,
            rateLimitGate ?? new TestRateLimitGate());
    }

    private static Fixture CreateFixture()
    {
        var memberStore = new FakeMemberStore();
        var admin = Member.Create(
            "ADMIN001",
            "Admin",
            "admin@example.org",
            Department.CoreDepartmentId,
            true,
            Guid.NewGuid(),
            MemberPosition.Admin);
        var mentor = Member.Create(
            "MENTOR001",
            "Mentor",
            "mentor@example.org",
            Guid.NewGuid(),
            false,
            Guid.NewGuid(),
            MemberPosition.Member);
        var regularMember = Member.Create(
            "MEMBER001",
            "Member",
            "member@example.org",
            Guid.NewGuid(),
            false,
            Guid.NewGuid(),
            MemberPosition.Member);
        memberStore.Members.AddRange([admin, mentor, regularMember]);

        var team = ProbationTeam.Create("Team One");
        var otherTeam = ProbationTeam.Create("Team Two");
        var peerReviewer = ProbationCandidate.Create("CAND001", "Peer Reviewer", Guid.NewGuid(), Guid.NewGuid(), team.Id);
        var target = ProbationCandidate.Create("CAND002", "Target", Guid.NewGuid(), Guid.NewGuid(), team.Id);
        var otherTeamCandidate = ProbationCandidate.Create("CAND003", "Other Team", Guid.NewGuid(), Guid.NewGuid(), otherTeam.Id);
        var period = EvaluationPeriod.Create("Week 1");
        var peerForm = EvaluationForm.Create(period.Id, "Peer form", EvaluationReviewerType.Peer);
        var mentorForm = EvaluationForm.Create(period.Id, "Mentor form", EvaluationReviewerType.Mentor);
        var scoreQuestion = EvaluationQuestion.Create(peerForm.Id, 1, "Score", EvaluationQuestionType.Score, true, 1, 5, null);
        var textQuestion = EvaluationQuestion.Create(mentorForm.Id, 1, "Note", EvaluationQuestionType.Text, true, null, null, 1000);
        var evaluationStore = new FakeEvaluationStore();
        evaluationStore.Periods.Add(period);
        evaluationStore.Forms.AddRange([peerForm, mentorForm]);
        evaluationStore.Questions.AddRange([scoreQuestion, textQuestion]);
        var probationStore = new FakeProbationTeamStore();
        probationStore.Candidates.AddRange([peerReviewer, target, otherTeamCandidate]);

        return new Fixture(
            evaluationStore,
            probationStore,
            memberStore,
            admin,
            mentor,
            regularMember,
            team,
            period,
            peerForm,
            mentorForm,
            scoreQuestion,
            textQuestion,
            peerReviewer,
            target,
            otherTeamCandidate);
    }

    private sealed record Fixture(
        FakeEvaluationStore EvaluationStore,
        FakeProbationTeamStore ProbationStore,
        FakeMemberStore MemberStore,
        Member Admin,
        Member Mentor,
        Member RegularMember,
        ProbationTeam Team,
        EvaluationPeriod Period,
        EvaluationForm PeerForm,
        EvaluationForm MentorForm,
        EvaluationQuestion ScoreQuestion,
        EvaluationQuestion TextQuestion,
        ProbationCandidate PeerReviewer,
        ProbationCandidate Target,
        ProbationCandidate OtherTeamCandidate);

    private sealed class DefaultsProvider : IEvaluationDefaultsProvider
    {
        public EvaluationDefaults GetDefaults() => new(1, 5, 2000);
    }

    private sealed class FakeEvaluationStore : IEvaluationStore
    {
        public List<EvaluationPeriod> Periods { get; } = [];

        public List<EvaluationForm> Forms { get; } = [];

        public List<EvaluationQuestion> Questions { get; } = [];

        public List<EvaluationSubmission> Submissions { get; } = [];

        public List<EvaluationAnswer> Answers { get; } = [];

        public Task<IReadOnlyList<EvaluationPeriodView>> ListPeriodsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<EvaluationPeriodView>>([]);

        public Task<EvaluationPeriodView?> FindPeriodViewAsync(Guid periodId, CancellationToken cancellationToken)
            => Task.FromResult<EvaluationPeriodView?>(null);

        public Task<EvaluationPeriod?> FindPeriodAsync(Guid periodId, bool track, CancellationToken cancellationToken)
            => Task.FromResult(Periods.SingleOrDefault(period => period.Id == periodId));

        public Task<EvaluationForm?> FindFormAsync(Guid formId, bool track, CancellationToken cancellationToken)
            => Task.FromResult(Forms.SingleOrDefault(form => form.Id == formId));

        public Task<IReadOnlyList<EvaluationQuestion>> ListQuestionsAsync(Guid formId, bool track, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<EvaluationQuestion>>(Questions.Where(question => question.FormId == formId).ToArray());

        public Task<EvaluationSubmission?> FindSubmissionAsync(Guid formId, Guid? reviewerMemberId, Guid? reviewerCandidateId, Guid targetCandidateId, bool track, CancellationToken cancellationToken)
            => Task.FromResult(Submissions.SingleOrDefault(submission => submission.FormId == formId
                && submission.ReviewerMemberId == reviewerMemberId
                && submission.ReviewerCandidateId == reviewerCandidateId
                && submission.TargetCandidateId == targetCandidateId));

        public Task<IReadOnlyList<EvaluationSubmissionView>> ListSubmissionViewsAsync(Guid? periodId, Guid? formId, CancellationToken cancellationToken)
        {
            var submissions = Submissions
                .Where(submission => periodId is null || submission.PeriodId == periodId)
                .Where(submission => formId is null || submission.FormId == formId)
                .Select(submission => new EvaluationSubmissionView(
                    submission.Id,
                    submission.FormId,
                    submission.PeriodId,
                    submission.ReviewerType,
                    submission.ReviewerMemberId,
                    submission.ReviewerCandidateId,
                    submission.ReviewerStudentIdSnapshot,
                    submission.ReviewerNameSnapshot,
                    submission.TargetCandidateId,
                    submission.TargetStudentIdSnapshot,
                    submission.TargetNameSnapshot,
                    submission.SubmittedAt,
                    submission.UpdatedAt,
                    Answers.Where(answer => answer.SubmissionId == submission.Id).Select(answer => new EvaluationAnswerView(
                        answer.Id,
                        answer.QuestionId,
                        answer.QuestionPromptSnapshot,
                        answer.QuestionTypeSnapshot,
                        answer.ScoreValue,
                        answer.TextValue,
                        answer.CreatedAt)).ToArray()))
                .ToArray();
            return Task.FromResult<IReadOnlyList<EvaluationSubmissionView>>(submissions);
        }

        public Task AddPeriodAsync(EvaluationPeriod period, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdatePeriodAsync(EvaluationPeriod period, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddFormAsync(EvaluationForm form, IReadOnlyCollection<EvaluationQuestion> questions, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateFormAsync(EvaluationForm form, IReadOnlyCollection<EvaluationQuestion> addedQuestions, IReadOnlyCollection<EvaluationQuestion> removedQuestions, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveSubmissionAsync(EvaluationSubmission submission, IReadOnlyCollection<EvaluationAnswer> answers, bool isNew, CancellationToken cancellationToken)
        {
            if (isNew)
            {
                Submissions.Add(submission);
            }
            else
            {
                Answers.RemoveAll(answer => answer.SubmissionId == submission.Id);
            }

            Answers.AddRange(answers);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProbationTeamStore : IProbationTeamStore
    {
        public List<ProbationCandidate> Candidates { get; } = [];

        public List<TeamMentor> Mentors { get; } = [];

        public Task<IReadOnlyList<ProbationTeamView>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ProbationTeamView>>([]);

        public Task<ProbationTeamView?> FindViewAsync(Guid teamId, CancellationToken cancellationToken)
            => Task.FromResult<ProbationTeamView?>(null);

        public Task<ProbationTeam?> FindTeamAsync(Guid teamId, bool track, CancellationToken cancellationToken)
            => Task.FromResult<ProbationTeam?>(null);

        public Task<ProbationCandidate?> FindCandidateAsync(Guid candidateId, bool track, CancellationToken cancellationToken)
            => Task.FromResult(Candidates.SingleOrDefault(candidate => candidate.Id == candidateId));

        public Task<TeamMentor?> FindMentorAsync(Guid teamId, Guid memberId, bool track, CancellationToken cancellationToken)
            => Task.FromResult(Mentors.SingleOrDefault(mentor => mentor.TeamId == teamId && mentor.MemberId == memberId));

        public Task<long?> FindCandidateDiscordUserIdAsync(Guid candidateId, CancellationToken cancellationToken)
            => Task.FromResult<long?>(null);

        public Task AddTeamAsync(ProbationTeam team, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateTeamAsync(ProbationTeam team, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveCandidateAssignmentAsync(ProbationCandidate candidate, AuditLog auditLog, DiscordSyncJob? syncJob, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddMentorAsync(TeamMentor mentor, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveMentorAsync(TeamMentor mentor, AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMemberStore : IMemberStore
    {
        public List<Member> Members { get; } = [];

        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
            => Task.FromResult(Members.SingleOrDefault(member => member.Id == memberId));

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
