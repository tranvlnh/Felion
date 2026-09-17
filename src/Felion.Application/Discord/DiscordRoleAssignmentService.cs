using System.Globalization;
using System.Text.Json;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;
using Felion.Domain.Members;

namespace Felion.Application.Discord;

public sealed class DiscordRoleAssignmentService(
    IDiscordRoleAssignmentStore store,
    IMemberStore memberStore,
    IDiscordGuildRoleCatalog? roleCatalog = null) : IDiscordRoleAssignmentService
{
    public async Task<DiscordRoleAssignmentDashboard> ListAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(actorMemberId, cancellationToken);
        var subjects = await store.ListSubjectsAsync(cancellationToken);
        return new DiscordRoleAssignmentDashboard(subjects.Select(ToDto).ToArray());
    }

    public async Task<IReadOnlyList<DiscordAssignableRoleDto>> ListRolesAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(actorMemberId, cancellationToken);
        var roles = await GetRoleCatalogAsync(cancellationToken);
        return roles
            .Where(role => !role.IsManaged && !role.IsEveryone)
            .OrderByDescending(role => role.RawPosition)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .Select(role => new DiscordAssignableRoleDto(
                role.Id.ToString(CultureInfo.InvariantCulture),
                role.Name,
                role.RawPosition))
            .ToArray();
    }

    public async Task<DiscordRoleAssignmentSubjectDto> ReplaceAsync(
        Guid actorMemberId,
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        UpdateDiscordRoleAssignmentsCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(actorMemberId, cancellationToken);
        if (!Enum.IsDefined(subjectType) || subjectId == Guid.Empty)
        {
            throw new DiscordRoleAssignmentValidationException("A valid role assignment subject is required.");
        }

        var subject = await store.FindSubjectAsync(subjectType, subjectId, cancellationToken)
            ?? throw new DiscordRoleAssignmentNotFoundException();
        var requestedRoleIds = NormalizeRequestedRoleIds(command.DiscordRoleIds);
        var roles = await GetRoleCatalogAsync(cancellationToken);
        var rolesById = roles.ToDictionary(role => role.Id);

        foreach (var roleId in requestedRoleIds)
        {
            if (!rolesById.TryGetValue(roleId, out var role))
            {
                throw new DiscordRoleAssignmentValidationException(
                    $"Discord role '{roleId}' does not exist in the configured guild.");
            }

            if (role.IsEveryone || role.IsManaged)
            {
                throw new DiscordRoleAssignmentValidationException(
                    $"Discord role '{role.Name}' is not assignable by this feature.");
            }
        }

        var desiredAssignments = requestedRoleIds
            .Select(roleId =>
            {
                try
                {
                    return DiscordRoleAssignment.Create(
                        subjectType,
                        subjectId,
                        roleId,
                        rolesById[roleId].Name);
                }
                catch (DomainException exception)
                {
                    throw new DiscordRoleAssignmentValidationException(exception.Message);
                }
            })
            .ToArray();
        var currentAssignments = subject.Assignments;
        if (HaveSameAssignments(currentAssignments, desiredAssignments))
        {
            return ToDto(subject with { Assignments = desiredAssignments });
        }

        var syncJob = subject.DiscordUserId is null
            ? null
            : DiscordSyncJob.Create(
                subjectType,
                subjectId,
                DiscordSyncOperation.SynchronizeRoles,
                JsonSerializer.Serialize(new
                {
                    DiscordUserId = subject.DiscordUserId.Value,
                    AdditionalManagedRoleIds = currentAssignments
                        .Select(assignment => assignment.DiscordRoleId)
                        .ToArray()
                }));
        var audit = AuditLog.Create(
            AuditActorType.WebMember,
            actorMemberId,
            actorDiscordUserId: null,
            action: "DiscordRoleAssignmentsUpdated",
            entityType: subjectType == DiscordIdentitySubjectType.Member
                ? "Member"
                : "ProbationCandidate",
            entityId: subjectId,
            correlationId,
            metadataJson: JsonSerializer.Serialize(new
            {
                SubjectType = subjectType,
                DiscordUserId = subject.DiscordUserId,
                AddedRoleIds = requestedRoleIds.Except(currentAssignments.Select(assignment => assignment.DiscordRoleId)),
                RemovedRoleIds = currentAssignments
                    .Select(assignment => assignment.DiscordRoleId)
                    .Except(requestedRoleIds),
                SyncJobId = syncJob?.Id
            }),
            beforeJson: JsonSerializer.Serialize(currentAssignments.Select(ToAuditSnapshot)),
            afterJson: JsonSerializer.Serialize(desiredAssignments.Select(ToAuditSnapshot)));

        await store.ReplaceAsync(
            subjectType,
            subjectId,
            currentAssignments.Select(assignment => assignment.DiscordRoleId).ToArray(),
            desiredAssignments,
            audit,
            syncJob,
            cancellationToken);

        return ToDto(subject with { Assignments = desiredAssignments });
    }

    private async Task<IReadOnlyList<DiscordGuildRoleSnapshot>> GetRoleCatalogAsync(
        CancellationToken cancellationToken)
    {
        if (roleCatalog is null)
        {
            throw new DiscordRoleUnavailableException();
        }

        return await roleCatalog.ListRolesAsync(cancellationToken);
    }

    private async Task EnsureAdminAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position != MemberPosition.Admin)
        {
            throw new DiscordRoleAssignmentAccessDeniedException();
        }
    }

    private static long[] NormalizeRequestedRoleIds(IReadOnlyCollection<long>? roleIds)
    {
        if (roleIds is null)
        {
            throw new DiscordRoleAssignmentValidationException("DiscordRoleIds is required.");
        }

        if (roleIds.Count > 100)
        {
            throw new DiscordRoleAssignmentValidationException("At most 100 Discord roles may be assigned.");
        }

        var normalized = roleIds.ToArray();
        if (normalized.Any(roleId => roleId <= 0))
        {
            throw new DiscordRoleAssignmentValidationException(
                "Discord role IDs must be positive signed snowflakes.");
        }

        if (normalized.Length != normalized.Distinct().Count())
        {
            throw new DiscordRoleAssignmentValidationException(
                "Each Discord role may be selected only once.");
        }

        return normalized;
    }

    private static bool HaveSameAssignments(
        IReadOnlyCollection<DiscordRoleAssignment> current,
        IReadOnlyCollection<DiscordRoleAssignment> desired)
    {
        return current
            .Select(assignment => (assignment.DiscordRoleId, assignment.RoleNameSnapshot))
            .OrderBy(value => value.DiscordRoleId)
            .SequenceEqual(desired
                .Select(assignment => (assignment.DiscordRoleId, assignment.RoleNameSnapshot))
                .OrderBy(value => value.DiscordRoleId));
    }

    private static DiscordRoleAssignmentSubjectDto ToDto(DiscordRoleAssignmentSubjectView subject)
    {
        return new DiscordRoleAssignmentSubjectDto(
            subject.SubjectId,
            subject.SubjectType,
            subject.StudentId,
            subject.FullName,
            subject.MemberPosition,
            subject.MemberStatus,
            subject.CandidateStatus,
            subject.DiscordUserId,
            subject.Assignments
                .OrderBy(assignment => assignment.RoleNameSnapshot, StringComparer.OrdinalIgnoreCase)
                .Select(assignment => new DiscordRoleAssignmentDto(
                    assignment.DiscordRoleId.ToString(CultureInfo.InvariantCulture),
                    assignment.RoleNameSnapshot))
                .ToArray());
    }

    private static object ToAuditSnapshot(DiscordRoleAssignment assignment)
    {
        return new
        {
            assignment.DiscordRoleId,
            assignment.RoleNameSnapshot
        };
    }
}
