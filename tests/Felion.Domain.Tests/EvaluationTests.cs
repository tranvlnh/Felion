using Felion.Domain.Common;
using Felion.Domain.Evaluation;

namespace Felion.Domain.Tests;

public sealed class EvaluationTests
{
    [Fact]
    public void PeriodStartsOpenAndCanOnlyBeClosedOnce()
    {
        var now = new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
        var period = EvaluationPeriod.Create(" Week 1 ", now);

        Assert.Equal("Week 1", period.Name);
        Assert.Equal(EvaluationPeriodStatus.Open, period.Status);
        Assert.Equal(now, period.OpenedAt);

        period.Close(now.AddDays(1));

        Assert.Equal(EvaluationPeriodStatus.Closed, period.Status);
        Assert.Equal(now.AddDays(1), period.ClosedAt);
        Assert.Throws<DomainException>(() => period.Close(now.AddDays(2)));
    }

    [Fact]
    public void PeerEvaluationEnforcesFixedScoresAndNoSelfReview()
    {
        var periodId = Guid.NewGuid();
        var evaluatorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var evaluation = PeerEvaluation.Create(
            periodId,
            evaluatorId,
            targetId,
            "P001",
            "Evaluator",
            "P002",
            "Target",
            4,
            5,
            3);

        Assert.Equal(4, evaluation.Contribution);
        evaluation.Update(5, 5, 4, "Good teammate");
        Assert.Equal("Good teammate", evaluation.Note);
        Assert.Throws<DomainException>(() => evaluation.Update(0, 5, 4));
        Assert.Throws<DomainException>(() => PeerEvaluation.Create(
            periodId,
            evaluatorId,
            evaluatorId,
            "P001",
            "Evaluator",
            "P001",
            "Evaluator",
            4,
            4,
            4));
    }

    [Fact]
    public void MentorEvaluationEnforcesTenPointScores()
    {
        var evaluation = MentorEvaluation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "M001",
            "Mentor",
            "P001",
            "Candidate",
            10,
            8,
            9);

        Assert.Equal(10, evaluation.Attendance);
        Assert.Throws<DomainException>(() => evaluation.Update(10, 0, 9));
    }
}
