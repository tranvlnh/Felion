using Felion.Domain.Common;
using Felion.Domain.Evaluation;

namespace Felion.Domain.Tests;

public sealed class EvaluationTests
{
    [Fact]
    public void PeriodTransitionsFromDraftToOpenToClosedOnly()
    {
        var period = EvaluationPeriod.Create(" Week 1 ");

        Assert.Equal(EvaluationPeriodStatus.Draft, period.Status);
        period.Open();
        Assert.Equal(EvaluationPeriodStatus.Open, period.Status);
        period.Close();
        Assert.Equal(EvaluationPeriodStatus.Closed, period.Status);
        Assert.Throws<DomainException>(() => period.Open());
        Assert.Throws<DomainException>(() => period.Close());
    }

    [Fact]
    public void PeriodRejectsAnInvalidSchedule()
    {
        var start = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(7));

        Assert.Throws<DomainException>(() => EvaluationPeriod.Create(
            "Week 1",
            start,
            start.AddMinutes(-1)));
    }

    [Fact]
    public void ScoreAndTextQuestionsRequireMatchingConfiguration()
    {
        var formId = Guid.NewGuid();

        var score = EvaluationQuestion.Create(
            formId,
            1,
            "Quality",
            EvaluationQuestionType.Score,
            true,
            1,
            5,
            null);
        var text = EvaluationQuestion.Create(
            formId,
            2,
            "Feedback",
            EvaluationQuestionType.Text,
            false,
            null,
            null,
            2000);

        Assert.Equal(5, score.ScoreMax);
        Assert.Equal(2000, text.TextMaxLength);
        Assert.Throws<DomainException>(() => EvaluationQuestion.Create(
            formId,
            3,
            "Invalid",
            EvaluationQuestionType.Score,
            true,
            5,
            1,
            null));
        Assert.Throws<DomainException>(() => EvaluationQuestion.Create(
            formId,
            3,
            "Invalid",
            EvaluationQuestionType.Text,
            true,
            1,
            5,
            2000));
    }

    [Fact]
    public void AnswersValidateValuesAndSnapshotQuestionDefinition()
    {
        var question = EvaluationQuestion.Create(
            Guid.NewGuid(),
            1,
            " Quality ",
            EvaluationQuestionType.Score,
            true,
            1,
            5,
            null);
        var submissionId = Guid.NewGuid();

        var answer = EvaluationAnswer.Create(submissionId, question, 4, null);

        Assert.Equal(question.Id, answer.QuestionId);
        Assert.Equal("Quality", answer.QuestionPromptSnapshot);
        Assert.Equal(EvaluationQuestionType.Score, answer.QuestionTypeSnapshot);
        Assert.Equal(4, answer.ScoreValue);
        Assert.Throws<DomainException>(() => EvaluationAnswer.Create(submissionId, question, 6, null));
        Assert.Throws<DomainException>(() => EvaluationAnswer.Create(submissionId, question, null, "not a score"));
    }

    [Fact]
    public void SubmissionRequiresReviewerMatchingReviewerType()
    {
        var formId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var submission = EvaluationSubmission.Create(
            formId,
            periodId,
            EvaluationReviewerType.Peer,
            null,
            Guid.NewGuid(),
            "CANDIDATE001",
            "Candidate",
            targetId,
            "TARGET001",
            "Target");

        Assert.Equal(targetId, submission.TargetCandidateId);
        Assert.Throws<DomainException>(() => EvaluationSubmission.Create(
            formId,
            periodId,
            EvaluationReviewerType.Peer,
            Guid.NewGuid(),
            null,
            "CANDIDATE001",
            "Candidate",
            targetId,
            "TARGET001",
            "Target"));
    }
}
