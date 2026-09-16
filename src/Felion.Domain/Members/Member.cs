using Felion.Domain.Common;

namespace Felion.Domain.Members;

public sealed class Member
{
    private Member()
    {
    }

    private Member(
        Guid id,
        string studentId,
        string fullName,
        string clubEmail,
        Guid departmentId,
        Guid generationId,
        MemberPosition position,
        DateTimeOffset now)
    {
        Id = id;
        StudentId = studentId;
        FullName = fullName;
        ClubEmail = clubEmail;
        DepartmentId = departmentId;
        GenerationId = generationId;
        Position = position;
        Status = MemberStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string StudentId { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string ClubEmail { get; private set; } = string.Empty;

    public Guid DepartmentId { get; private set; }

    public Guid GenerationId { get; private set; }

    public MemberPosition Position { get; private set; }

    public MemberStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Member Create(
        string studentId,
        string fullName,
        string clubEmail,
        Guid departmentId,
        bool isCoreDepartment,
        Guid generationId,
        MemberPosition position,
        DateTimeOffset? now = null)
    {
        var normalizedStudentId = IdentityNormalizer.StudentId(studentId);
        var normalizedName = IdentityNormalizer.RequiredText(fullName, nameof(FullName));
        var normalizedEmail = IdentityNormalizer.ClubEmail(clubEmail);
        ValidateIds(departmentId, generationId);
        ValidatePositionDepartment(position, isCoreDepartment);

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new Member(
            Guid.NewGuid(),
            normalizedStudentId,
            normalizedName,
            normalizedEmail,
            departmentId,
            generationId,
            position,
            timestamp);
    }

    public void ChangePosition(MemberPosition position, bool isCoreDepartment, DateTimeOffset? now = null)
    {
        ValidatePositionDepartment(position, isCoreDepartment);
        Position = position;
        Touch(now);
    }

    public void UpdateProfile(
        string studentId,
        string fullName,
        string clubEmail,
        Guid departmentId,
        bool isCoreDepartment,
        Guid generationId,
        MemberPosition position,
        DateTimeOffset? now = null)
    {
        var normalizedStudentId = IdentityNormalizer.StudentId(studentId);
        var normalizedName = IdentityNormalizer.RequiredText(fullName, nameof(FullName));
        var normalizedEmail = IdentityNormalizer.ClubEmail(clubEmail);
        ValidateIds(departmentId, generationId);
        ValidatePositionDepartment(position, isCoreDepartment);

        StudentId = normalizedStudentId;
        FullName = normalizedName;
        ClubEmail = normalizedEmail;
        DepartmentId = departmentId;
        GenerationId = generationId;
        Position = position;
        Touch(now);
    }

    public void ChangeStatus(MemberStatus status, DateTimeOffset? now = null)
    {
        Status = status;
        Touch(now);
    }

    public void Deactivate(DateTimeOffset? now = null)
    {
        Status = MemberStatus.Inactive;
        Touch(now);
    }

    public void Reactivate(DateTimeOffset? now = null)
    {
        Status = MemberStatus.Active;
        Touch(now);
    }

    private static void ValidateIds(Guid departmentId, Guid generationId)
    {
        if (departmentId == Guid.Empty)
        {
            throw new DomainException("Department is required.");
        }

        if (generationId == Guid.Empty)
        {
            throw new DomainException("Generation is required.");
        }
    }

    private static void ValidatePositionDepartment(MemberPosition position, bool isCoreDepartment)
    {
        var requiresCoreDepartment = position is MemberPosition.Admin or MemberPosition.Core;
        if (requiresCoreDepartment != isCoreDepartment)
        {
            throw new DomainException(
                "Admin and Core members must use the Core department; regular Members must not use it.");
        }
    }

    private void Touch(DateTimeOffset? now)
    {
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }
}
