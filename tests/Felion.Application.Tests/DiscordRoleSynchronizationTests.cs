using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Tests;

public sealed class DiscordRoleSynchronizationTests
{
    [Fact]
    public async Task SynchronizeAllReadsEveryActiveLinkedIdentityAndComputesDesiredRoles()
    {
        var departmentId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var store = new FakeStore();
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Position,
            "Member",
            10,
            "Member"));
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Department,
            departmentId.ToString("D"),
            20,
            "Technical"));
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Generation,
            generationId.ToString("D"),
            30,
            "Generation 1"));
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Probation,
            "Probation",
            40,
            "Probation"));
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.ProbationTeam,
            teamId.ToString("D"),
            60,
            "Team Alpha"));
        store.Targets.Add(new DiscordRoleSyncTarget(
            123456789,
            DiscordIdentitySubjectType.Member,
            MemberPosition.Member,
            departmentId,
            generationId,
            null,
            memberId));
        store.Targets.Add(new DiscordRoleSyncTarget(
            987654321,
            DiscordIdentitySubjectType.Probation,
            null,
            departmentId,
            generationId,
            teamId,
            candidateId));
        store.Assignments[(DiscordIdentitySubjectType.Member, memberId)] = [
            DiscordRoleAssignment.Create(
                DiscordIdentitySubjectType.Member,
                memberId,
                50,
                "Individual")];
        store.Assignments[(DiscordIdentitySubjectType.Probation, candidateId)] = [
            DiscordRoleAssignment.Create(
                DiscordIdentitySubjectType.Probation,
                candidateId,
                70,
                "Candidate custom")];
        var gateway = new FakeRoleGateway();
        var service = new DiscordRoleSynchronizationService(store, gateway);

        var result = await service.SynchronizeAllAsync(
            Guid.NewGuid(),
            111222333,
            "correlation-sync-all",
            CancellationToken.None);

        Assert.Equal(2, result.TotalSubjects);
        Assert.Equal(2, result.SynchronizedSubjects);
        Assert.Empty(result.Failures);
        var audit = Assert.Single(store.Audits);
        Assert.Equal("DiscordRolesSynchronized", audit.Action);
        Assert.Equal(111222333, audit.ActorDiscordUserId);
        Assert.Equal([123456789L, 987654321L], gateway.Calls.Select(call => call.DiscordUserId));
        Assert.Equal([10L, 20L, 30L, 50L], gateway.Calls[0].DesiredRoleIds.OrderBy(id => id));
        Assert.Equal([10L, 20L, 30L, 40L, 50L, 60L], gateway.Calls[0].ManagedRoleIds.OrderBy(id => id));
        Assert.Equal([20L, 30L, 40L, 60L, 70L], gateway.Calls[1].DesiredRoleIds.OrderBy(id => id));
        Assert.Equal([10L, 20L, 30L, 40L, 60L, 70L], gateway.Calls[1].ManagedRoleIds.OrderBy(id => id));
    }

    [Fact]
    public async Task SynchronizeSubjectClearsMappedAndAssignedRolesWhenIdentityIsNoLongerActive()
    {
        var subjectId = Guid.NewGuid();
        var store = new FakeStore();
        store.Mappings.Add(DiscordRoleMapping.Create(
            DiscordRoleMappingKind.Position,
            "Member",
            10,
            "Member"));
        store.Assignments[(DiscordIdentitySubjectType.Member, subjectId)] = [
            DiscordRoleAssignment.Create(
                DiscordIdentitySubjectType.Member,
                subjectId,
                20,
                "Individual")];
        store.DiscordUserIds[(DiscordIdentitySubjectType.Member, subjectId)] = 123456789;
        var gateway = new FakeRoleGateway();
        var service = new DiscordRoleSynchronizationService(store, gateway);

        await service.SynchronizeSubjectAsync(
            DiscordIdentitySubjectType.Member,
            subjectId,
            CancellationToken.None);

        var call = Assert.Single(gateway.Calls);
        Assert.Equal(123456789, call.DiscordUserId);
        Assert.Empty(call.DesiredRoleIds);
        Assert.Equal([10L, 20L], call.ManagedRoleIds.OrderBy(id => id));
    }

    private sealed class FakeStore : IDiscordRoleSynchronizationStore
    {
        public List<DiscordRoleMapping> Mappings { get; } = [];

        public List<AuditLog> Audits { get; } = [];

        public List<DiscordRoleSyncTarget> Targets { get; } = [];

        public Dictionary<(DiscordIdentitySubjectType SubjectType, Guid SubjectId), long> DiscordUserIds { get; } = [];

        public Dictionary<(DiscordIdentitySubjectType SubjectType, Guid SubjectId), IReadOnlyList<DiscordRoleAssignment>> Assignments { get; } = [];

        public Task<IReadOnlyList<DiscordRoleMapping>> ListRoleMappingsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DiscordRoleMapping>>(Mappings);

        public Task RecordAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            Audits.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DiscordRoleSyncTarget>> ListLinkedActiveTargetsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DiscordRoleSyncTarget>>(Targets);

        public Task<DiscordRoleSyncTarget?> FindTargetAsync(
            DiscordIdentitySubjectType subjectType,
            Guid subjectId,
            CancellationToken cancellationToken)
            => Task.FromResult(Targets.SingleOrDefault(target =>
                target.SubjectType == subjectType && target.SubjectId == subjectId));

        public Task<long?> FindDiscordUserIdAsync(
            DiscordIdentitySubjectType subjectType,
            Guid subjectId,
            CancellationToken cancellationToken)
            => Task.FromResult(DiscordUserIds.TryGetValue((subjectType, subjectId), out var userId)
                ? (long?)userId
                : null);

        public Task<IReadOnlyList<DiscordRoleAssignment>> ListRoleAssignmentsAsync(
            DiscordIdentitySubjectType subjectType,
            Guid subjectId,
            CancellationToken cancellationToken)
            => Task.FromResult(Assignments.TryGetValue((subjectType, subjectId), out var assignments)
                ? assignments
                : (IReadOnlyList<DiscordRoleAssignment>)[]);
    }

    private sealed class FakeRoleGateway : IDiscordRoleGateway
    {
        public List<Call> Calls { get; } = [];

        public Task<DiscordRoleSnapshot> GetRoleAsync(long discordRoleId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<DiscordRoleSnapshot> CreateRoleAsync(string name, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task SynchronizeUserRolesAsync(
            long discordUserId,
            IReadOnlyCollection<long> desiredRoleIds,
            IReadOnlyCollection<long> managedRoleIds,
            CancellationToken cancellationToken)
        {
            Calls.Add(new Call(discordUserId, desiredRoleIds.ToArray(), managedRoleIds.ToArray()));
            return Task.CompletedTask;
        }
    }

    private sealed record Call(
        long DiscordUserId,
        IReadOnlyCollection<long> DesiredRoleIds,
        IReadOnlyCollection<long> ManagedRoleIds);
}
