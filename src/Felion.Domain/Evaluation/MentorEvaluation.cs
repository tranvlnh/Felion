using Felion.Domain.Common;

namespace Felion.Domain.Evaluation;

public sealed class MentorEvaluation
{
    private MentorEvaluation()
    {
    }

    private MentorEvaluation(
        Guid id,
        Guid evaluationPeriodId,
        Guid mentorMemberId,
        Guid targetCandidateId,
        string mentorStudentIdSnapshot,
        string mentorNameSnapshot,
        string targetStudentIdSnapshot,
        string targetNameSnapshot,
        int attendance,
        int taskCompletion,
        int learningInitiative,
        string? note,
        DateTimeOffset now)
    {
        Id = id;
        EvaluationPeriodId = evaluationPeriodId;
        MentorMemberId = mentorMemberId;
        TargetCandidateId = targetCandidateId;
        MentorStudentIdSnapshot = mentorStudentIdSnapshot;
        MentorNameSnapshot = mentorNameSnapshot;
        TargetStudentIdSnapshot = targetStudentIdSnapshot;
        TargetNameSnapshot = targetNameSnapshot;
        Attendance = attendance;
        TaskCompletion = taskCompletion;
        LearningInitiative = learningInitiative;
        Note = note;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid EvaluationPeriodId { get; private set; }

    public Guid MentorMemberId { get; private set; }

    public Guid TargetCandidateId { get; private set; }

    public string MentorStudentIdSnapshot { get; private set; } = string.Empty;

    public string MentorNameSnapshot { get; private set; } = string.Empty;

    public string TargetStudentIdSnapshot { get; private set; } = string.Empty;

    public string TargetNameSnapshot { get; private set; } = string.Empty;

    public int Attendance { get; private set; }

    public int TaskCompletion { get; private set; }

    public int LearningInitiative { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static MentorEvaluation Create(
        Guid evaluationPeriodId,
        Guid mentorMemberId,
        Guid targetCandidateId,
        string mentorStudentIdSnapshot,
        string mentorNameSnapshot,
        string targetStudentIdSnapshot,
        string targetNameSnapshot,
        int attendance,
        int taskCompletion,
        int learningInitiative,
        string? note = null,
        DateTimeOffset? now = null)
    {
        ValidateIds(evaluationPeriodId, mentorMemberId, targetCandidateId);
        ValidateScores(attendance, taskCompletion, learningInitiative);
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var snapshots = NormalizeSnapshots(
            mentorStudentIdSnapshot,
            mentorNameSnapshot,
            targetStudentIdSnapshot,
            targetNameSnapshot);
        return new MentorEvaluation(
            Guid.NewGuid(),
            evaluationPeriodId,
            mentorMemberId,
            targetCandidateId,
            snapshots.MentorStudentId,
            snapshots.MentorName,
            snapshots.TargetStudentId,
            snapshots.TargetName,
            attendance,
            taskCompletion,
            learningInitiative,
            NormalizeNote(note),
            timestamp);
    }

    public void Update(
        int attendance,
        int taskCompletion,
        int learningInitiative,
        string? note = null,
        DateTimeOffset? now = null)
    {
        ValidateScores(attendance, taskCompletion, learningInitiative);
        Attendance = attendance;
        TaskCompletion = taskCompletion;
        LearningInitiative = learningInitiative;
        Note = NormalizeNote(note);
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    private static void ValidateIds(Guid evaluationPeriodId, Guid mentorMemberId, Guid targetCandidateId)
    {
        if (evaluationPeriodId == Guid.Empty)
        {
            throw new DomainException("Evaluation period is required.");
        }

        if (mentorMemberId == Guid.Empty)
        {
            throw new DomainException("Mentor member is required.");
        }

        if (targetCandidateId == Guid.Empty)
        {
            throw new DomainException("Target candidate is required.");
        }
    }

    private static void ValidateScores(int attendance, int taskCompletion, int learningInitiative)
    {
        if (attendance is < 1 or > 10)
        {
            throw new DomainException("Attendance must be between 1 and 10.");
        }

        if (taskCompletion is < 1 or > 10)
        {
            throw new DomainException("TaskCompletion must be between 1 and 10.");
        }

        if (learningInitiative is < 1 or > 10)
        {
            throw new DomainException("LearningInitiative must be between 1 and 10.");
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

    private static (string MentorStudentId, string MentorName, string TargetStudentId, string TargetName) NormalizeSnapshots(
        string mentorStudentId,
        string mentorName,
        string targetStudentId,
        string targetName)
    {
        return (
            IdentityNormalizer.StudentId(mentorStudentId),
            NormalizeName(mentorName, "Mentor"),
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
