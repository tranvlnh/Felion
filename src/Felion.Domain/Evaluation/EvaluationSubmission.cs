using Felion.Domain.Common;

namespace Felion.Domain.Evaluation;

public sealed class EvaluationSubmission
{
    private EvaluationSubmission()
    {
    }

    private EvaluationSubmission(
        Guid id,
        Guid formId,
        Guid periodId,
        EvaluationReviewerType reviewerType,
        Guid? reviewerMemberId,
        Guid? reviewerCandidateId,
        string reviewerStudentIdSnapshot,
        string reviewerNameSnapshot,
        Guid targetCandidateId,
        string targetStudentIdSnapshot,
        string targetNameSnapshot,
        DateTimeOffset now)
    {
        Id = id;
        FormId = formId;
        PeriodId = periodId;
        ReviewerType = reviewerType;
        ReviewerMemberId = reviewerMemberId;
        ReviewerCandidateId = reviewerCandidateId;
        ReviewerStudentIdSnapshot = reviewerStudentIdSnapshot;
        ReviewerNameSnapshot = reviewerNameSnapshot;
        TargetCandidateId = targetCandidateId;
        TargetStudentIdSnapshot = targetStudentIdSnapshot;
        TargetNameSnapshot = targetNameSnapshot;
        SubmittedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid FormId { get; private set; }

    public Guid PeriodId { get; private set; }

    public EvaluationReviewerType ReviewerType { get; private set; }

    public Guid? ReviewerMemberId { get; private set; }

    public Guid? ReviewerCandidateId { get; private set; }

    public string ReviewerStudentIdSnapshot { get; private set; } = string.Empty;

    public string ReviewerNameSnapshot { get; private set; } = string.Empty;

    public Guid? TargetCandidateId { get; private set; }

    public string TargetStudentIdSnapshot { get; private set; } = string.Empty;

    public string TargetNameSnapshot { get; private set; } = string.Empty;

    public DateTimeOffset SubmittedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static EvaluationSubmission Create(
        Guid formId,
        Guid periodId,
        EvaluationReviewerType reviewerType,
        Guid? reviewerMemberId,
        Guid? reviewerCandidateId,
        string reviewerStudentIdSnapshot,
        string reviewerNameSnapshot,
        Guid targetCandidateId,
        string targetStudentIdSnapshot,
        string targetNameSnapshot,
        DateTimeOffset? now = null)
    {
        ValidateIds(formId, periodId, targetCandidateId);
        ValidateReviewer(reviewerType, reviewerMemberId, reviewerCandidateId);

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new EvaluationSubmission(
            Guid.NewGuid(),
            formId,
            periodId,
            reviewerType,
            reviewerMemberId,
            reviewerCandidateId,
            NormalizeStudentIdSnapshot(reviewerStudentIdSnapshot),
            NormalizeNameSnapshot(reviewerNameSnapshot, "Reviewer"),
            targetCandidateId,
            NormalizeStudentIdSnapshot(targetStudentIdSnapshot),
            NormalizeNameSnapshot(targetNameSnapshot, "Target"),
            timestamp);
    }

    public void UpdateSnapshots(
        Guid targetCandidateId,
        string reviewerStudentIdSnapshot,
        string reviewerNameSnapshot,
        string targetStudentIdSnapshot,
        string targetNameSnapshot,
        DateTimeOffset? now = null)
    {
        if (targetCandidateId == Guid.Empty)
        {
            throw new DomainException("Target candidate is required.");
        }

        TargetCandidateId = targetCandidateId;
        ReviewerStudentIdSnapshot = NormalizeStudentIdSnapshot(reviewerStudentIdSnapshot);
        ReviewerNameSnapshot = NormalizeNameSnapshot(reviewerNameSnapshot, "Reviewer");
        TargetStudentIdSnapshot = NormalizeStudentIdSnapshot(targetStudentIdSnapshot);
        TargetNameSnapshot = NormalizeNameSnapshot(targetNameSnapshot, "Target");
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    private static void ValidateIds(Guid formId, Guid periodId, Guid targetCandidateId)
    {
        if (formId == Guid.Empty)
        {
            throw new DomainException("Evaluation form is required.");
        }

        if (periodId == Guid.Empty)
        {
            throw new DomainException("Evaluation period is required.");
        }

        if (targetCandidateId == Guid.Empty)
        {
            throw new DomainException("Target candidate is required.");
        }
    }

    private static void ValidateReviewer(
        EvaluationReviewerType reviewerType,
        Guid? reviewerMemberId,
        Guid? reviewerCandidateId)
    {
        var hasMemberReviewer = reviewerMemberId is not null;
        var hasCandidateReviewer = reviewerCandidateId is not null;
        if (reviewerType == EvaluationReviewerType.Peer && (!hasCandidateReviewer || hasMemberReviewer))
        {
            throw new DomainException("Peer evaluations require a candidate reviewer.");
        }

        if (reviewerType == EvaluationReviewerType.Mentor && (!hasMemberReviewer || hasCandidateReviewer))
        {
            throw new DomainException("Mentor evaluations require a member reviewer.");
        }

        if (reviewerType is not (EvaluationReviewerType.Peer or EvaluationReviewerType.Mentor))
        {
            throw new DomainException("Unknown evaluation reviewer type.");
        }

        if (reviewerMemberId == Guid.Empty || reviewerCandidateId == Guid.Empty)
        {
            throw new DomainException("Reviewer identifier cannot be empty.");
        }
    }

    private static string NormalizeStudentIdSnapshot(string value)
    {
        return IdentityNormalizer.StudentId(value);
    }

    private static string NormalizeNameSnapshot(string value, string subject)
    {
        var normalized = IdentityNormalizer.RequiredText(value, subject + "NameSnapshot");
        if (normalized.Length > 200)
        {
            throw new DomainException($"{subject} name snapshot must be at most 200 characters.");
        }

        return normalized;
    }
}
