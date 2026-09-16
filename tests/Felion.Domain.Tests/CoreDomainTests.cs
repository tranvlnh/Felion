using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Domain.Tests;

public sealed class CoreDomainTests
{
    private static readonly Guid GenerationId = new("a9c0a9ef-bad2-4ee7-9d59-9d6b13c3d1ca");
    private static readonly Guid RegularDepartmentId = new("ce5bd8f5-7b0b-4f80-9d22-e51b458b4e41");

    [Fact]
    public void IdentityValuesAreNormalizedBeforePersistence()
    {
        var member = Member.Create(
            " sv001 ",
            " Student One ",
            " MEMBER@EXAMPLE.ORG ",
            RegularDepartmentId,
            isCoreDepartment: false,
            GenerationId,
            MemberPosition.Member);

        Assert.Equal("SV001", member.StudentId);
        Assert.Equal("member@example.org", member.ClubEmail);
        Assert.Equal("Student One", member.FullName);
    }

    [Fact]
    public void AdminAndCoreMustUseCoreDepartment()
    {
        var exception = Assert.Throws<DomainException>(() => Member.Create(
            "SV001",
            "Student One",
            "member@example.org",
            RegularDepartmentId,
            isCoreDepartment: false,
            GenerationId,
            MemberPosition.Core));

        Assert.Contains("Core department", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegularMemberCannotUseCoreDepartment()
    {
        var exception = Assert.Throws<DomainException>(() => Member.Create(
            "SV001",
            "Student One",
            "member@example.org",
            Department.CoreDepartmentId,
            isCoreDepartment: true,
            GenerationId,
            MemberPosition.Member));

        Assert.Contains("Core department", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProbationCandidateDecisionIsOneWayUntilArchived()
    {
        var candidate = ProbationCandidate.Create(
            "SV001",
            "Student One",
            RegularDepartmentId,
            GenerationId);

        candidate.MarkPassed();
        candidate.Archive();

        Assert.Equal(ProbationCandidateStatus.Archived, candidate.Status);
        Assert.Throws<DomainException>(() => candidate.MarkFailed());
    }

    [Fact]
    public void DiscordLinkRejectsNonPositiveSnowflakesAndNormalizesStudentId()
    {
        var link = DiscordIdentityLink.Create(
            123456789,
            " sv001 ",
            DiscordIdentitySubjectType.Member,
            Guid.NewGuid());

        Assert.Equal("SV001", link.StudentId);
        Assert.Throws<DomainException>(() => DiscordIdentityLink.Create(
            0,
            "SV001",
            DiscordIdentitySubjectType.Member,
            Guid.NewGuid()));
    }

    [Fact]
    public void AuditLogRequiresValidActorAndJson()
    {
        var memberId = Guid.NewGuid();
        var audit = AuditLog.Create(
            AuditActorType.WebMember,
            memberId,
            null,
            "MemberCreated",
            "Member",
            memberId,
            "correlation-1",
            "{\"source\":\"test\"}");

        Assert.Equal("{\"source\":\"test\"}", audit.MetadataJson);
        Assert.Throws<DomainException>(() => AuditLog.Create(
            AuditActorType.WebMember,
            memberId,
            123456789,
            "MemberCreated",
            "Member",
            memberId,
            "correlation-1"));
        Assert.Throws<DomainException>(() => AuditLog.Create(
            AuditActorType.System,
            null,
            null,
            "MemberCreated",
            "Member",
            memberId,
            "correlation-1",
            "not-json"));
    }

    [Fact]
    public void DiscordSyncJobTransitionsAreExplicitAndRetryable()
    {
        var job = DiscordSyncJob.Create(
            DiscordIdentitySubjectType.Member,
            Guid.NewGuid(),
            DiscordSyncOperation.SynchronizeRoles,
            "{\"subjectId\":\"test\"}");

        job.MarkRunning();
        job.MarkFailed("Discord role synchronization failed.");
        job.MarkRunning();
        job.MarkSucceeded();

        Assert.Equal(DiscordSyncJobStatus.Succeeded, job.Status);
        Assert.Equal(2, job.Attempts);
        Assert.Null(job.LastError);
    }

    [Fact]
    public void DiscordRoleMappingCanonicalizesLogicalSubjectKeys()
    {
        var position = DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Position,
            " member ",
            123456789,
            "Members");
        var departmentId = Guid.NewGuid();
        var department = DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Department,
            departmentId.ToString("D").ToUpperInvariant(),
            987654321,
            "Technical");

        Assert.Equal("Member", position.SubjectKey);
        Assert.Equal(departmentId.ToString("D"), department.SubjectKey);
        Assert.Throws<DomainException>(() => DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Probation,
            "Candidate",
            123456789,
            "Probation"));
    }
}
