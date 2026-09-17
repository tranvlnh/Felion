using System.Text.Json;
using Felion.Domain.Audit;
using Felion.Domain.Identity;

namespace Felion.Application.Discord;

public sealed class DiscordRoleSynchronizationService(
    IDiscordRoleSynchronizationStore store,
    IDiscordRoleGateway roleGateway) : IDiscordRoleSynchronizationService
{
    public Task SynchronizeSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        return SynchronizeSubjectAsync(subjectType, subjectId, [], cancellationToken);
    }

    public async Task SynchronizeSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        IReadOnlyCollection<long> additionalManagedRoleIds,
        CancellationToken cancellationToken)
    {
        var mappings = await store.ListRoleMappingsAsync(cancellationToken);
        var target = await store.FindTargetAsync(subjectType, subjectId, cancellationToken);
        var assignments = await store.ListRoleAssignmentsAsync(subjectType, subjectId, cancellationToken);
        var managedRoleIds = BuildManagedRoleIds(mappings, assignments, additionalManagedRoleIds);

        if (target is null)
        {
            var discordUserId = await store.FindDiscordUserIdAsync(
                subjectType,
                subjectId,
                cancellationToken);
            if (discordUserId is not > 0)
            {
                return;
            }

            await roleGateway.SynchronizeUserRolesAsync(
                discordUserId.Value,
                [],
                managedRoleIds,
                cancellationToken);
            return;
        }

        await SynchronizeTargetAsync(
            target,
            mappings,
            assignments,
            additionalManagedRoleIds,
            cancellationToken);
    }

    public async Task ClearSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        long discordUserId,
        CancellationToken cancellationToken)
    {
        var mappings = await store.ListRoleMappingsAsync(cancellationToken);
        var assignments = await store.ListRoleAssignmentsAsync(subjectType, subjectId, cancellationToken);
        var managedRoleIds = BuildManagedRoleIds(mappings, assignments, []);

        await roleGateway.SynchronizeUserRolesAsync(
            discordUserId,
            [],
            managedRoleIds,
            cancellationToken);
    }

    public async Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        CancellationToken cancellationToken)
    {
        return await SynchronizeAllAsync([], cancellationToken);
    }

    public async Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        IReadOnlyCollection<long> additionalManagedRoleIds,
        CancellationToken cancellationToken)
    {
        var mappings = await store.ListRoleMappingsAsync(cancellationToken);
        var targets = await store.ListLinkedActiveTargetsAsync(cancellationToken);
        var failures = new List<DiscordRoleSyncFailure>();
        var synchronizedSubjects = 0;

        foreach (var target in targets)
        {
            try
            {
                var assignments = await store.ListRoleAssignmentsAsync(
                    target.SubjectType,
                    target.SubjectId,
                    cancellationToken);
                await SynchronizeTargetAsync(
                    target,
                    mappings,
                    assignments,
                    additionalManagedRoleIds,
                    cancellationToken);
                synchronizedSubjects++;
            }
            catch (DiscordRoleGatewayException exception)
            {
                failures.Add(new DiscordRoleSyncFailure(
                    target.SubjectId,
                    target.SubjectType,
                    target.DiscordUserId,
                    exception.Message));
            }
        }

        return new DiscordRoleSyncAllResult(
            targets.Count,
            synchronizedSubjects,
            failures);
    }

    public async Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        Guid actorMemberId,
        long actorDiscordUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var audit = AuditLog.Create(
            AuditActorType.DiscordMember,
            actorMemberId: null,
            actorDiscordUserId,
            "DiscordRolesSynchronized",
            "DiscordGuild",
            entityId: null,
            correlationId,
            metadataJson: JsonSerializer.Serialize(new
            {
                Scope = "ActiveLinkedIdentities",
                FelionMemberId = actorMemberId
            }));
        await store.RecordAuditAsync(audit, cancellationToken);
        return await SynchronizeAllAsync(cancellationToken);
    }

    private async Task SynchronizeTargetAsync(
        DiscordRoleSyncTarget target,
        IReadOnlyCollection<DiscordRoleMapping> mappings,
        IReadOnlyCollection<DiscordRoleAssignment> assignments,
        IReadOnlyCollection<long> additionalManagedRoleIds,
        CancellationToken cancellationToken)
    {
        var managedRoleIds = BuildManagedRoleIds(mappings, assignments, additionalManagedRoleIds);
        var desiredRoleIds = mappings
            .Where(mapping => IsDesired(mapping, target))
            .Select(mapping => mapping.DiscordRoleId)
            .Concat(assignments.Select(assignment => assignment.DiscordRoleId))
            .ToHashSet();

        await roleGateway.SynchronizeUserRolesAsync(
            target.DiscordUserId,
            desiredRoleIds,
            managedRoleIds,
            cancellationToken);
    }

    private static HashSet<long> BuildManagedRoleIds(
        IReadOnlyCollection<DiscordRoleMapping> mappings,
        IReadOnlyCollection<DiscordRoleAssignment> assignments,
        IReadOnlyCollection<long> additionalManagedRoleIds)
    {
        return mappings
            .Select(mapping => mapping.DiscordRoleId)
            .Concat(assignments.Select(assignment => assignment.DiscordRoleId))
            .Concat(additionalManagedRoleIds.Where(roleId => roleId > 0))
            .ToHashSet();
    }

    private static bool IsDesired(DiscordRoleMapping mapping, DiscordRoleSyncTarget target)
    {
        return mapping.Kind switch
        {
            DiscordRoleMappingKind.Position => target.SubjectType == DiscordIdentitySubjectType.Member
                && target.Position?.ToString() == mapping.SubjectKey,
            DiscordRoleMappingKind.Probation => target.SubjectType == DiscordIdentitySubjectType.Probation,
            DiscordRoleMappingKind.Department => target.DepartmentId.ToString("D") == mapping.SubjectKey,
            DiscordRoleMappingKind.Generation => target.GenerationId.ToString("D") == mapping.SubjectKey,
            DiscordRoleMappingKind.ProbationTeam => target.ProbationTeamId?.ToString("D") == mapping.SubjectKey,
            _ => false
        };
    }
}

public sealed class DisabledDiscordRoleSynchronizationService : IDiscordRoleSynchronizationService
{
    public Task SynchronizeSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task SynchronizeSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        IReadOnlyCollection<long> additionalManagedRoleIds,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task ClearSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        long discordUserId,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new DiscordRoleSyncAllResult(0, 0, []));
    }

    public Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        IReadOnlyCollection<long> additionalManagedRoleIds,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new DiscordRoleSyncAllResult(0, 0, []));
    }

    public Task<DiscordRoleSyncAllResult> SynchronizeAllAsync(
        Guid actorMemberId,
        long actorDiscordUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        return SynchronizeAllAsync(cancellationToken);
    }
}
