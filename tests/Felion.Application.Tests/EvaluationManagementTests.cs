using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Evaluation;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class EvaluationManagementTests
{
    [Fact]
    public async Task CreatePeriodWritesAuditAndStartsDraft()
    {
        var fixture = CreateFixture();
        var service = new EvaluationManagementService(fixture.Store, fixture.MemberStore, fixture.Defaults);

        var result = await service.CreatePeriodAsync(
            fixture.Admin.Id,
            new CreateEvaluationPeriodCommand(" Week 1 "),
            "evaluation-period-create",
            CancellationToken.None);

        Assert.Equal("Week 1", result.Name);
        Assert.Equal(EvaluationPeriodStatus.Draft, result.Status);
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "EvaluationPeriodCreated");
    }

    [Fact]
    public async Task OpenAndClosePeriodWriteAuditsAndEnforceLifecycle()
    {
        var fixture = CreateFixture();
        var service = new EvaluationManagementService(fixture.Store, fixture.MemberStore, fixture.Defaults);
        var period = EvaluationPeriod.Create("Week 1");
        fixture.Store.Periods.Add(period);

        var open = await service.OpenPeriodAsync(
            fixture.Admin.Id,
            period.Id,
            "evaluation-open",
            CancellationToken.None);
        var closed = await service.ClosePeriodAsync(
            fixture.Admin.Id,
            period.Id,
            "evaluation-close",
            CancellationToken.None);

        Assert.Equal(EvaluationPeriodStatus.Open, open.Status);
        Assert.Equal(EvaluationPeriodStatus.Closed, closed.Status);
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "EvaluationPeriodOpened");
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "EvaluationPeriodClosed");
        await Assert.ThrowsAsync<EvaluationValidationException>(() => service.OpenPeriodAsync(
            fixture.Admin.Id,
            period.Id,
            "evaluation-reopen",
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateFormAppliesConfiguredDefaultsAndWritesAudit()
    {
        var fixture = CreateFixture();
        var period = EvaluationPeriod.Create("Week 1");
        fixture.Store.Periods.Add(period);
        var service = new EvaluationManagementService(fixture.Store, fixture.MemberStore, fixture.Defaults);

        var form = await service.CreateFormAsync(
            fixture.Admin.Id,
            new CreateEvaluationFormCommand(
                period.Id,
                "Peer form",
                EvaluationReviewerType.Peer,
                [
                    new EvaluationQuestionCommand(1, "Score", EvaluationQuestionType.Score, true),
                    new EvaluationQuestionCommand(2, "Note", EvaluationQuestionType.Text, false)
                ]),
            "evaluation-form-create",
            CancellationToken.None);

        var score = Assert.Single(form.Questions, question => question.Type == EvaluationQuestionType.Score);
        var text = Assert.Single(form.Questions, question => question.Type == EvaluationQuestionType.Text);
        Assert.Equal(1, score.ScoreMin);
        Assert.Equal(10, score.ScoreMax);
        Assert.Equal(4000, text.TextMaxLength);
        Assert.Contains(fixture.Store.AuditLogs, audit => audit.Action == "EvaluationFormCreated");
    }

    [Fact]
    public async Task DuplicateQuestionOrderIsRejectedBeforePersistence()
    {
        var fixture = CreateFixture();
        var period = EvaluationPeriod.Create("Week 1");
        fixture.Store.Periods.Add(period);
        var service = new EvaluationManagementService(fixture.Store, fixture.MemberStore, fixture.Defaults);

        await Assert.ThrowsAsync<EvaluationValidationException>(() => service.CreateFormAsync(
            fixture.Admin.Id,
            new CreateEvaluationFormCommand(
                period.Id,
                "Invalid form",
                EvaluationReviewerType.Mentor,
                [
                    new EvaluationQuestionCommand(1, "One", EvaluationQuestionType.Text, true),
                    new EvaluationQuestionCommand(1, "Two", EvaluationQuestionType.Text, true)
                ]),
            "evaluation-form-invalid",
            CancellationToken.None));
        Assert.Empty(fixture.Store.Forms);
        Assert.Empty(fixture.Store.AuditLogs);
    }

    [Fact]
    public async Task RegularMemberCannotManageEvaluations()
    {
        var fixture = CreateFixture();
        var service = new EvaluationManagementService(fixture.Store, fixture.MemberStore, fixture.Defaults);

        await Assert.ThrowsAsync<EvaluationAccessDeniedException>(() => service.ListPeriodsAsync(
            fixture.RegularMember.Id,
            CancellationToken.None));
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
        var regularMember = Member.Create(
            "MEMBER001",
            "Member",
            "member@example.org",
            Guid.NewGuid(),
            false,
            Guid.NewGuid(),
            MemberPosition.Member);
        memberStore.Members.Add(admin);
        memberStore.Members.Add(regularMember);
        var defaults = new FakeEvaluationDefaultsProvider(new EvaluationDefaults(1, 10, 4000));
        return new Fixture(memberStore, new FakeEvaluationStore(), defaults, admin, regularMember);
    }

    private sealed record Fixture(
        FakeMemberStore MemberStore,
        FakeEvaluationStore Store,
        FakeEvaluationDefaultsProvider Defaults,
        Member Admin,
        Member RegularMember);

    private sealed class FakeEvaluationDefaultsProvider(EvaluationDefaults defaults) : IEvaluationDefaultsProvider
    {
        public EvaluationDefaults GetDefaults() => defaults;
    }

    private sealed class FakeEvaluationStore : IEvaluationStore
    {
        public List<EvaluationPeriod> Periods { get; } = [];

        public List<EvaluationForm> Forms { get; } = [];

        public List<EvaluationQuestion> Questions { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public Task<EvaluationSubmission?> FindSubmissionAsync(
            Guid formId,
            Guid? reviewerMemberId,
            Guid? reviewerCandidateId,
            Guid targetCandidateId,
            bool track,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<EvaluationSubmission?>(null);
        }

        public Task<IReadOnlyList<EvaluationSubmissionView>> ListSubmissionViewsAsync(
            Guid? periodId,
            Guid? formId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EvaluationSubmissionView>>([]);
        }

        public Task<IReadOnlyList<EvaluationPeriodView>> ListPeriodsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EvaluationPeriodView>>(Periods.Select(ToView).ToArray());
        }

        public Task<EvaluationPeriodView?> FindPeriodViewAsync(Guid periodId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Periods.SingleOrDefault(period => period.Id == periodId) is { } period
                ? (EvaluationPeriodView?)ToView(period)
                : null);
        }

        public Task<EvaluationPeriod?> FindPeriodAsync(Guid periodId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Periods.SingleOrDefault(period => period.Id == periodId));
        }

        public Task<EvaluationForm?> FindFormAsync(Guid formId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult(Forms.SingleOrDefault(form => form.Id == formId));
        }

        public Task<IReadOnlyList<EvaluationQuestion>> ListQuestionsAsync(Guid formId, bool track, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EvaluationQuestion>>(Questions
                .Where(question => question.FormId == formId)
                .OrderBy(question => question.Order)
                .ToArray());
        }

        public Task AddPeriodAsync(EvaluationPeriod period, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Periods.Add(period);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task UpdatePeriodAsync(EvaluationPeriod period, AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddFormAsync(EvaluationForm form, IReadOnlyCollection<EvaluationQuestion> questions, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Forms.Add(form);
            Questions.AddRange(questions);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task UpdateFormAsync(EvaluationForm form, IReadOnlyCollection<EvaluationQuestion> addedQuestions, IReadOnlyCollection<EvaluationQuestion> removedQuestions, AuditLog auditLog, CancellationToken cancellationToken)
        {
            Questions.RemoveAll(question => removedQuestions.Contains(question));
            Questions.AddRange(addedQuestions);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveSubmissionAsync(
            EvaluationSubmission submission,
            IReadOnlyCollection<EvaluationAnswer> answers,
            bool isNew,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private EvaluationPeriodView ToView(EvaluationPeriod period)
        {
            return new EvaluationPeriodView(
                period.Id,
                period.Name,
                period.StartsAt,
                period.EndsAt,
                period.Status,
                Forms.Where(form => form.PeriodId == period.Id).Select(ToView).ToArray(),
                period.CreatedAt,
                period.UpdatedAt);
        }

        private EvaluationFormView ToView(EvaluationForm form)
        {
            return new EvaluationFormView(
                form.Id,
                form.PeriodId,
                form.Name,
                form.ReviewerType,
                form.IsActive,
                Questions.Where(question => question.FormId == form.Id).Select(ToView).ToArray(),
                form.CreatedAt,
                form.UpdatedAt);
        }

        private static EvaluationQuestionView ToView(EvaluationQuestion question)
        {
            return new EvaluationQuestionView(
                question.Id,
                question.FormId,
                question.Order,
                question.Prompt,
                question.Type,
                question.IsRequired,
                question.ScoreMin,
                question.ScoreMax,
                question.TextMaxLength,
                question.CreatedAt,
                question.UpdatedAt);
        }
    }

    private sealed class FakeMemberStore : IMemberStore
    {
        public List<Member> Members { get; } = [];

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
