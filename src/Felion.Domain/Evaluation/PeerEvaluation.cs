using Felion.Domain.Common;

namespace Felion.Domain.Evaluation;

public sealed class PeerEvaluation
{
    private PeerEvaluation()
    {
    }

    private PeerEvaluation(
        Guid id,
        Guid evaluationPeriodId,
        Guid evaluatorCandidateId,
        Guid targetCandidateId,
        string evaluatorStudentIdSnapshot,
        string evaluatorNameSnapshot,
        string targetStudentIdSnapshot,
        string targetNameSnapshot,
        int contribution,
        int communication,
        int attitude,
        string? note,
        DateTimeOffset now)
    {
        Id = id;
        EvaluationPeriodId = evaluationPeriodId;
        EvaluatorCandidateId = evaluatorCandidateId;
        TargetCandidateId = targetCandidateId;
        EvaluatorStudentIdSnapshot = evaluatorStudentIdSnapshot;
        EvaluatorNameSnapshot = evaluatorNameSnapshot;
        TargetStudentIdSnapshot = targetStudentIdSnapshot;
        TargetNameSnapshot = targetNameSnapshot;
        Contribution = contribution;
        Communication = communication;
        Attitude = attitude;
        Note = note;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid EvaluationPeriodId { get; private set; }

    public Guid EvaluatorCandidateId { get; private set; }

    public Guid TargetCandidateId { get; private set; }

    public string EvaluatorStudentIdSnapshot { get; private set; } = string.Empty;

    public string EvaluatorNameSnapshot { get; private set; } = string.Empty;

    public string TargetStudentIdSnapshot { get; private set; } = string.Empty;

    public string TargetNameSnapshot { get; private set; } = string.Empty;

    public int Contribution { get; private set; }

    public int Communication { get; private set; }

    public int Attitude { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static PeerEvaluation Create(
        Guid evaluationPeriodId,
        Guid evaluatorCandidateId,
        Guid targetCandidateId,
        string evaluatorStudentIdSnapshot,
        string evaluatorNameSnapshot,
        string targetStudentIdSnapshot,
        string targetNameSnapshot,
        int contribution,
        int communication,
        int attitude,
        string? note = null,
        DateTimeOffset? now = null)
    {
        ValidateIds(evaluationPeriodId, evaluatorCandidateId, targetCandidateId);
        ValidateScores(contribution, communication, attitude);
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var snapshots = NormalizeSnapshots(
            evaluatorStudentIdSnapshot,
            evaluatorNameSnapshot,
            targetStudentIdSnapshot,
            targetNameSnapshot);
        return new PeerEvaluation(
            Guid.NewGuid(),
            evaluationPeriodId,
            evaluatorCandidateId,
            targetCandidateId,
            snapshots.EvaluatorStudentId,
            snapshots.EvaluatorName,
            snapshots.TargetStudentId,
            snapshots.TargetName,
            contribution,
            communication,
            attitude,
            NormalizeNote(note),
            timestamp);
    }

    public void Update(
        int contribution,
        int communication,
        int attitude,
        string? note = null,
        DateTimeOffset? now = null)
    {
        ValidateScores(contribution, communication, attitude);
        Contribution = contribution;
        Communication = communication;
        Attitude = attitude;
        Note = NormalizeNote(note);
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    private static void ValidateIds(Guid evaluationPeriodId, Guid evaluatorCandidateId, Guid targetCandidateId)
    {
        if (evaluationPeriodId == Guid.Empty)
        {
            throw new DomainException("Evaluation period is required.");
        }

        if (evaluatorCandidateId == Guid.Empty)
        {
            throw new DomainException("Evaluator candidate is required.");
        }

        if (targetCandidateId == Guid.Empty)
        {
            throw new DomainException("Target candidate is required.");
        }

        if (evaluatorCandidateId == targetCandidateId)
        {
            throw new DomainException("A candidate cannot evaluate themselves.");
        }
    }

    private static void ValidateScores(int contribution, int communication, int attitude)
    {
        if (contribution is < 1 or > 5)
        {
            throw new DomainException("Contribution must be between 1 and 5.");
        }

        if (communication is < 1 or > 5)
        {
            throw new DomainException("Communication must be between 1 and 5.");
        }

        if (attitude is < 1 or > 5)
        {
            throw new DomainException("Attitude must be between 1 and 5.");
        }
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var normalized = note.Trim();
        if (normalized.Length > 4000)
        {
            throw new DomainException("Evaluation note must be at most 4000 characters.");
        }

        return normalized;
    }

    private static (string EvaluatorStudentId, string EvaluatorName, string TargetStudentId, string TargetName) NormalizeSnapshots(
        string evaluatorStudentId,
        string evaluatorName,
        string targetStudentId,
        string targetName)
    {
        return (
            IdentityNormalizer.StudentId(evaluatorStudentId),
            NormalizeName(evaluatorName, "Evaluator"),
            IdentityNormalizer.StudentId(targetStudentId),
            NormalizeName(targetName, "Target"));
    }

    private static string NormalizeName(string value, string subject)
    {
        var normalized = IdentityNormalizer.RequiredText(value, subject + "NameSnapshot");
        if (normalized.Length > 200)
        {
            throw new DomainException($"{subject} name snapshot must be at most 200 characters.");
        }

        return normalized;
    }
}
