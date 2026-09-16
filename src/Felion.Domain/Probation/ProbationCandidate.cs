using Felion.Domain.Common;

namespace Felion.Domain.Probation;

public sealed class ProbationCandidate
{
    private ProbationCandidate()
    {
    }

    private ProbationCandidate(
        Guid id,
        string studentId,
        string fullName,
        Guid departmentId,
        Guid generationId,
        Guid? teamId,
        DateTimeOffset now)
    {
        Id = id;
        StudentId = studentId;
        FullName = fullName;
        DepartmentId = departmentId;
        GenerationId = generationId;
        TeamId = teamId;
        Status = ProbationCandidateStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string StudentId { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public Guid DepartmentId { get; private set; }

    public Guid GenerationId { get; private set; }

    public Guid? TeamId { get; private set; }

    public ProbationCandidateStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ProbationCandidate Create(
        string studentId,
        string fullName,
        Guid departmentId,
        Guid generationId,
        Guid? teamId = null,
        DateTimeOffset? now = null)
    {
        var normalizedStudentId = IdentityNormalizer.StudentId(studentId);
        var normalizedName = IdentityNormalizer.RequiredText(fullName, nameof(FullName));
        ValidateIds(departmentId, generationId, teamId);

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new ProbationCandidate(
            Guid.NewGuid(),
            normalizedStudentId,
            normalizedName,
            departmentId,
            generationId,
            teamId,
            timestamp);
    }

    public void AssignToTeam(Guid teamId, DateTimeOffset? now = null)
    {
        EnsureActive();
        if (teamId == Guid.Empty)
        {
            throw new DomainException("Team is required.");
        }

        TeamId = teamId;
        Touch(now);
    }

    public void RemoveFromTeam(DateTimeOffset? now = null)
    {
        EnsureActive();
        TeamId = null;
        Touch(now);
    }

    public void MarkPassed(DateTimeOffset? now = null)
    {
        EnsureActive();
        Status = ProbationCandidateStatus.Passed;
        Touch(now);
    }

    public void MarkFailed(DateTimeOffset? now = null)
    {
        EnsureActive();
        Status = ProbationCandidateStatus.Failed;
        Touch(now);
    }

    public void Archive(DateTimeOffset? now = null)
    {
        if (Status is ProbationCandidateStatus.Active or ProbationCandidateStatus.Archived)
        {
            throw new DomainException("Only a decided probation candidate can be archived.");
        }

        Status = ProbationCandidateStatus.Archived;
        Touch(now);
    }

    private static void ValidateIds(Guid departmentId, Guid generationId, Guid? teamId)
    {
        if (departmentId == Guid.Empty)
        {
            throw new DomainException("Department is required.");
        }

        if (generationId == Guid.Empty)
        {
            throw new DomainException("Generation is required.");
        }

        if (teamId == Guid.Empty)
        {
            throw new DomainException("Team cannot be an empty identifier.");
        }
    }

    private void EnsureActive()
    {
        if (Status != ProbationCandidateStatus.Active)
        {
            throw new DomainException("Only an active probation candidate can be changed.");
        }
    }

    private void Touch(DateTimeOffset? now)
    {
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
