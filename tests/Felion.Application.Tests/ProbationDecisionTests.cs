using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Application.Probation;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Tests;

public sealed class ProbationDecisionTests
{
    [Fact]
    public async Task PassCreatesMemberTransfersLinkArchivesCandidateAndQueuesRoleSync()
    {
        var fixture = CreateFixture(new ProbationRetentionPolicy());
        fixture.DecisionStore.RoleAssignments.Add(DiscordRoleAssignment.Create(
            DiscordIdentitySubjectType.Probation,
            fixture.Candidate.Id,
            987654321,
            "Personal role"));
        var service = fixture.CreateService();

        var result = await service.DecideAsync(
            fixture.Admin.Id,
            [new ProbationDecisionRequest(fixture.Candidate.Id, ProbationDecision.Pass)],
            "correlation-pass",
            CancellationToken.None);

        var item = Assert.Single(result);
        Assert.True(item.Succeeded);
        var member = Assert.Single(fixture.DecisionStore.Members);
        Assert.Equal("vinhth@gdscptit.dev", member.ClubEmail);
        Assert.Equal(MemberPosition.Member, member.Position);
        Assert.Equal(ProbationCandidateStatus.Archived, fixture.Candidate.Status);
        Assert.Equal(DiscordIdentitySubjectType.Member, fixture.IdentityLink.SubjectType);
        Assert.Equal(member.Id, fixture.IdentityLink.SubjectId);
        var assignment = Assert.Single(fixture.DecisionStore.RoleAssignments);
        Assert.Equal(DiscordIdentitySubjectType.Member, assignment.SubjectType);
        Assert.Equal(member.Id, assignment.SubjectId);
        var syncJob = Assert.Single(fixture.DecisionStore.SyncJobs);
        Assert.Equal(DiscordSyncOperation.SynchronizeRoles, syncJob.Operation);
        Assert.Contains(fixture.DecisionStore.AuditLogs, audit => audit.Action == "ProbationCandidatePassed");
    }

    [Fact]
    public async Task PassDeletePolicyRemovesCandidateButKeepsAuditAndEvaluationIndependent()
    {
        var fixture = CreateFixture(new ProbationRetentionPolicy(ProbationSuccessPolicy.Delete));
        var service = fixture.CreateService();

        var result = await service.DecideAsync(
            fixture.Admin.Id,
            [new ProbationDecisionRequest(fixture.Candidate.Id, ProbationDecision.Pass)],
            "correlation-pass-delete",
            CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.Empty(fixture.DecisionStore.Candidates);
        Assert.Contains(fixture.DecisionStore.AuditLogs, audit => audit.Action == "ProbationCandidatePassed");
        Assert.Single(fixture.DecisionStore.Members);
    }

    [Fact]
    public async Task FailMarkInactiveRemovesLinkAndQueuesIdempotentKick()
    {
        var fixture = CreateFixture(new ProbationRetentionPolicy(FailurePolicy: ProbationFailurePolicy.MarkInactive));
        fixture.DecisionStore.RoleAssignments.Add(DiscordRoleAssignment.Create(
            DiscordIdentitySubjectType.Probation,
            fixture.Candidate.Id,
            987654321,
            "Personal role"));
        var service = fixture.CreateService();

        var result = await service.DecideAsync(
            fixture.Admin.Id,
            [new ProbationDecisionRequest(fixture.Candidate.Id, ProbationDecision.Fail)],
            "correlation-fail",
            CancellationToken.None);

        var item = Assert.Single(result);
        Assert.True(item.Succeeded);
        Assert.True(item.KickQueued);
        Assert.Equal(ProbationCandidateStatus.Failed, fixture.Candidate.Status);
        Assert.Empty(fixture.DecisionStore.IdentityLinks);
        Assert.Empty(fixture.DecisionStore.RoleAssignments);
        Assert.Equal(DiscordSyncOperation.KickUser, Assert.Single(fixture.DecisionStore.SyncJobs).Operation);
        Assert.Contains(fixture.DecisionStore.AuditLogs, audit => audit.Action == "ProbationCandidateFailed");
    }

    [Fact]
    public async Task FailDeletePolicyRemovesCandidateAndPreservesAudit()
    {
        var fixture = CreateFixture(new ProbationRetentionPolicy(FailurePolicy: ProbationFailurePolicy.Delete));
        var service = fixture.CreateService();

        var result = await service.DecideAsync(
            fixture.Admin.Id,
            [new ProbationDecisionRequest(fixture.Candidate.Id, ProbationDecision.Fail)],
            "correlation-fail-delete",
            CancellationToken.None);

        Assert.True(Assert.Single(result).Succeeded);
        Assert.Empty(fixture.DecisionStore.Candidates);
        Assert.Empty(fixture.DecisionStore.IdentityLinks);
        Assert.Contains(fixture.DecisionStore.AuditLogs, audit => audit.Action == "ProbationCandidateFailed");
    }

    [Fact]
    public async Task RegularMemberCannotDecideProbation()
    {
        var fixture = CreateFixture(new ProbationRetentionPolicy());
        var service = fixture.CreateService();

        await Assert.ThrowsAsync<ProbationDecisionAccessDeniedException>(() => service.DecideAsync(
            fixture.RegularMember.Id,
            [new ProbationDecisionRequest(fixture.Candidate.Id, ProbationDecision.Pass)],
            "correlation-denied",
            CancellationToken.None));
    }

    [Fact]
    public async Task BulkDecisionReturnsPerItemFailureWhenGeneratedEmailConflicts()
    {
        var fixture = CreateFixture(new ProbationRetentionPolicy());
        fixture.DecisionStore.Members.Add(Member.Create(
            "OTHER001",
            "Other",
            "vinhth@gdscptit.dev",
            fixture.RegularDepartment.Id,
            isCoreDepartment: false,
            fixture.Generation.Id,
            MemberPosition.Member));
        var service = fixture.CreateService();

        var result = await service.DecideAsync(
            fixture.Admin.Id,
            [new ProbationDecisionRequest(fixture.Candidate.Id, ProbationDecision.Pass)],
            "correlation-conflict",
            CancellationToken.None);

        var item = Assert.Single(result);
        Assert.False(item.Succeeded);
        Assert.Contains("already used", item.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProbationCandidateStatus.Active, fixture.Candidate.Status);
        Assert.Empty(fixture.DecisionStore.AuditLogs);
    }

    private static Fixture CreateFixture(ProbationRetentionPolicy policy)
    {
        var coreDepartment = Department.CreateCore();
        var regularDepartment = Department.CreateRegular("Technical", "technical");
        var generation = Generation.Create("Generation 1", "G1");
        var admin = Member.Create(
            "ADMIN001",
            "Admin",
            "admin@gdscptit.dev",
            coreDepartment.Id,
            isCoreDepartment: true,
            generation.Id,
            MemberPosition.Admin);
        var regularMember = Member.Create(
            "MEMBER001",
            "Mentor",
            "mentor@gdscptit.dev",
            regularDepartment.Id,
            isCoreDepartment: false,
            generation.Id,
            MemberPosition.Member);
        var candidate = ProbationCandidate.Create(
            "SV001",
            "Trần Hữu Vinh",
            regularDepartment.Id,
            generation.Id);
        var identityLink = DiscordIdentityLink.Create(
            123456789,
            candidate.StudentId,
            DiscordIdentitySubjectType.Probation,
            candidate.Id);
        var memberStore = new FakeMemberStore(coreDepartment, regularDepartment, generation, admin, regularMember);
        var decisionStore = new FakeDecisionStore(candidate, identityLink);
        return new Fixture(
            memberStore,
            decisionStore,
            new FakeRetentionPolicyProvider(policy),
            new FakeEmailGenerator("vinhth@gdscptit.dev"),
            admin,
            regularMember,
            candidate,
            identityLink,
            regularDepartment,
            generation);
    }

    private sealed record Fixture(
        FakeMemberStore MemberStore,
        FakeDecisionStore DecisionStore,
        FakeRetentionPolicyProvider RetentionPolicyProvider,
        FakeEmailGenerator EmailGenerator,
        Member Admin,
        Member RegularMember,
        ProbationCandidate Candidate,
        DiscordIdentityLink IdentityLink,
        Department RegularDepartment,
        Generation Generation)
    {
        public ProbationDecisionService CreateService()
        {
            return new ProbationDecisionService(
                DecisionStore,
                MemberStore,
                RetentionPolicyProvider,
                EmailGenerator);
        }
    }

    private sealed class FakeRetentionPolicyProvider(ProbationRetentionPolicy policy)
        : IProbationRetentionPolicyProvider
    {
        public ProbationRetentionPolicy GetPolicy() => policy;
    }

    private sealed class FakeEmailGenerator(string email) : IWorkspaceEmailGenerator
    {
        public string Generate(string fullName) => email;
    }

    private sealed class FakeDecisionStore(
        ProbationCandidate candidate,
        DiscordIdentityLink identityLink) : IProbationDecisionStore
    {
        public List<ProbationCandidate> Candidates { get; } = [candidate];

        public List<DiscordIdentityLink> IdentityLinks { get; } = [identityLink];

        public List<Member> Members { get; } = [];

        public List<DiscordSyncJob> SyncJobs { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public List<DiscordRoleAssignment> RoleAssignments { get; } = [];

        public Task<ProbationDecisionCandidateView?> FindCandidateAsync(
            Guid candidateId,
            bool track,
            CancellationToken cancellationToken)
        {
            var found = Candidates.SingleOrDefault(candidate => candidate.Id == candidateId);
            var link = IdentityLinks.SingleOrDefault(link => link.SubjectId == candidateId);
            return Task.FromResult(found is null ? null : new ProbationDecisionCandidateView(found, link));
        }

        public Task<bool> MemberStudentIdExistsAsync(string studentId, CancellationToken cancellationToken)
            => Task.FromResult(Members.Any(member => member.StudentId == studentId));

        public Task<bool> MemberClubEmailExistsAsync(string clubEmail, CancellationToken cancellationToken)
            => Task.FromResult(Members.Any(member => member.ClubEmail == clubEmail));

        public Task<IReadOnlyList<DiscordRoleAssignment>> ListRoleAssignmentsAsync(
            DiscordIdentitySubjectType subjectType,
            Guid subjectId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DiscordRoleAssignment>>(RoleAssignments
                .Where(assignment => assignment.SubjectType == subjectType && assignment.SubjectId == subjectId)
                .ToArray());

        public Task SavePassAsync(
            ProbationCandidate candidate,
            Member member,
            DiscordIdentityLink? identityLink,
            DiscordSyncJob? syncJob,
            AuditLog auditLog,
            bool deleteCandidate,
            CancellationToken cancellationToken)
        {
            Members.Add(member);
            if (deleteCandidate)
            {
                Candidates.Remove(candidate);
            }

            if (syncJob is not null)
            {
                SyncJobs.Add(syncJob);
            }

            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveFailAsync(
            ProbationCandidate candidate,
            DiscordIdentityLink? identityLink,
            DiscordSyncJob? kickJob,
            AuditLog auditLog,
            bool deleteCandidate,
            CancellationToken cancellationToken)
        {
            if (identityLink is not null)
            {
                IdentityLinks.Remove(identityLink);
            }

            RoleAssignments.RemoveAll(assignment =>
                assignment.SubjectType == DiscordIdentitySubjectType.Probation
                && assignment.SubjectId == candidate.Id);

            if (deleteCandidate)
            {
                Candidates.Remove(candidate);
            }

            if (kickJob is not null)
            {
                SyncJobs.Add(kickJob);
            }

            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMemberStore(
        Department coreDepartment,
        Department regularDepartment,
        Generation generation,
        Member admin,
        Member regularMember) : IMemberStore
    {
        public Task<Member?> FindByIdAsync(Guid memberId, bool track, CancellationToken cancellationToken)
            => Task.FromResult<Member?>(memberId == admin.Id ? admin : memberId == regularMember.Id ? regularMember : null);

        public Task<(IReadOnlyList<Member> Items, int TotalCount)> ListAsync(int page, int pageSize, MemberStatus? status, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<MemberIdentityLookup> FindIdentityConflictsAsync(IReadOnlyCollection<string> studentIds, IReadOnlyCollection<string> clubEmails, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Department?> FindDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
            => Task.FromResult<Department?>(departmentId == coreDepartment.Id ? coreDepartment : departmentId == regularDepartment.Id ? regularDepartment : null);

        public Task<Generation?> FindGenerationAsync(Guid generationId, CancellationToken cancellationToken)
            => Task.FromResult<Generation?>(generationId == generation.Id ? generation : null);

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
