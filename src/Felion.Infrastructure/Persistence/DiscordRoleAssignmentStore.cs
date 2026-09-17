using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felion.Infrastructure.Persistence;

internal sealed class DiscordRoleAssignmentStore(FelionDbContext dbContext) : IDiscordRoleAssignmentStore
{
    public async Task<IReadOnlyList<DiscordRoleAssignmentSubjectView>> ListSubjectsAsync(
        CancellationToken cancellationToken)
    {
        var memberRows = await (
            from member in dbContext.Members.AsNoTracking()
            join link in dbContext.DiscordIdentityLinks.AsNoTracking()
                    .Where(link => link.SubjectType == DiscordIdentitySubjectType.Member)
                on member.Id equals link.SubjectId into linkJoin
            from link in linkJoin.DefaultIfEmpty()
            where member.Status == MemberStatus.Active
            orderby member.StudentId
            select new
            {
                member.Id,
                member.StudentId,
                member.FullName,
                member.Position,
                member.Status,
                member.DepartmentId,
                member.GenerationId,
                DiscordUserId = link == null ? null : (long?)link.DiscordUserId
            })
            .ToListAsync(cancellationToken);
        var roleMappings = await dbContext.DiscordRoleMappings
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var members = memberRows
            .Select(row => new DiscordRoleAssignmentSubjectView(
                row.Id,
                DiscordIdentitySubjectType.Member,
                row.StudentId,
                row.FullName,
                row.Position,
                row.Status,
                CandidateStatus: null,
                row.DiscordUserId,
                Assignments: Array.Empty<DiscordRoleAssignment>(),
                AutomaticRoles: GetAutomaticRoles(
                    DiscordIdentitySubjectType.Member,
                    row.Position,
                    row.DepartmentId,
                    row.GenerationId,
                    probationTeamId: null,
                    roleMappings)))
            .ToArray();

        var candidateRows = await (
            from candidate in dbContext.ProbationCandidates.AsNoTracking()
            join link in dbContext.DiscordIdentityLinks.AsNoTracking()
                    .Where(link => link.SubjectType == DiscordIdentitySubjectType.Probation)
                on candidate.Id equals link.SubjectId into linkJoin
            from link in linkJoin.DefaultIfEmpty()
            where candidate.Status == ProbationCandidateStatus.Active
            orderby candidate.StudentId
            select new
            {
                candidate.Id,
                candidate.StudentId,
                candidate.FullName,
                candidate.Status,
                candidate.DepartmentId,
                candidate.GenerationId,
                candidate.TeamId,
                DiscordUserId = link == null ? null : (long?)link.DiscordUserId
            })
            .ToListAsync(cancellationToken);
        var candidates = candidateRows
            .Select(row => new DiscordRoleAssignmentSubjectView(
                row.Id,
                DiscordIdentitySubjectType.Probation,
                row.StudentId,
                row.FullName,
                MemberPosition: null,
                MemberStatus: null,
                row.Status,
                row.DiscordUserId,
                Assignments: Array.Empty<DiscordRoleAssignment>(),
                AutomaticRoles: GetAutomaticRoles(
                    DiscordIdentitySubjectType.Probation,
                    null,
                    row.DepartmentId,
                    row.GenerationId,
                    row.TeamId,
                    roleMappings)))
            .ToArray();

        var subjects = members.Concat(candidates).ToArray();
        var memberIds = members.Select(subject => subject.SubjectId).ToArray();
        var candidateIds = candidates.Select(subject => subject.SubjectId).ToArray();
        var assignments = await dbContext.DiscordRoleAssignments
            .AsNoTracking()
            .Where(assignment =>
                (assignment.SubjectType == DiscordIdentitySubjectType.Member
                    && memberIds.Contains(assignment.SubjectId))
                || (assignment.SubjectType == DiscordIdentitySubjectType.Probation
                    && candidateIds.Contains(assignment.SubjectId)))
            .ToListAsync(cancellationToken);

        return subjects
            .Select(subject => subject with
            {
                Assignments = assignments
                    .Where(assignment =>
                        assignment.SubjectType == subject.SubjectType
                        && assignment.SubjectId == subject.SubjectId)
                    .OrderBy(assignment => assignment.RoleNameSnapshot)
                    .ToArray()
            })
            .ToArray();
    }

    private static DiscordRoleProjection[] GetAutomaticRoles(
        DiscordIdentitySubjectType subjectType,
        MemberPosition? position,
        Guid departmentId,
        Guid generationId,
        Guid? probationTeamId,
        IReadOnlyCollection<DiscordRoleMapping> mappings)
    {
        var departmentKey = departmentId.ToString("D");
        var generationKey = generationId.ToString("D");
        var teamKey = probationTeamId?.ToString("D");

        return mappings
            .Where(mapping => mapping.Kind switch
            {
                DiscordRoleMappingKind.Position => subjectType == DiscordIdentitySubjectType.Member
                    && position?.ToString() == mapping.SubjectKey,
                DiscordRoleMappingKind.Probation => subjectType == DiscordIdentitySubjectType.Probation
                    && mapping.SubjectKey == nameof(DiscordIdentitySubjectType.Probation),
                DiscordRoleMappingKind.Department => mapping.SubjectKey == departmentKey,
                DiscordRoleMappingKind.Generation => mapping.SubjectKey == generationKey,
                DiscordRoleMappingKind.ProbationTeam => subjectType == DiscordIdentitySubjectType.Probation
                    && mapping.SubjectKey == teamKey,
                _ => false
            })
            .GroupBy(mapping => mapping.DiscordRoleId)
            .Select(group => new DiscordRoleProjection(
                group.Key,
                group.First().RoleNameSnapshot))
            .OrderBy(role => role.RoleNameSnapshot)
            .ToArray();
    }

    public async Task<DiscordRoleAssignmentSubjectView?> FindSubjectAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var subjects = await ListSubjectsAsync(cancellationToken);
        return subjects.SingleOrDefault(subject =>
            subject.SubjectType == subjectType && subject.SubjectId == subjectId);
    }

    public async Task ReplaceAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        IReadOnlyCollection<long> expectedRoleIds,
        IReadOnlyCollection<DiscordRoleAssignment> desiredAssignments,
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var subjectExists = subjectType switch
            {
                DiscordIdentitySubjectType.Member => await dbContext.Members.AnyAsync(
                    member => member.Id == subjectId && member.Status == MemberStatus.Active,
                    cancellationToken),
                DiscordIdentitySubjectType.Probation => await dbContext.ProbationCandidates.AnyAsync(
                    candidate => candidate.Id == subjectId && candidate.Status == ProbationCandidateStatus.Active,
                    cancellationToken),
                _ => false
            };
            if (!subjectExists)
            {
                throw new DiscordRoleAssignmentNotFoundException();
            }

            var currentAssignments = await dbContext.DiscordRoleAssignments
                .Where(assignment =>
                    assignment.SubjectType == subjectType && assignment.SubjectId == subjectId)
                .ToListAsync(cancellationToken);
            var expected = expectedRoleIds.ToHashSet();
            var actual = currentAssignments
                .Select(assignment => assignment.DiscordRoleId)
                .ToHashSet();
            if (!actual.SetEquals(expected))
            {
                throw new DiscordRoleAssignmentConflictException(
                    "The Discord role assignments were changed by another request. Refresh and try again.");
            }

            dbContext.DiscordRoleAssignments.RemoveRange(currentAssignments);
            dbContext.DiscordRoleAssignments.AddRange(desiredAssignments);
            dbContext.AuditLogs.Add(auditLog);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DiscordRoleAssignmentNotFoundException
            or DiscordRoleAssignmentConflictException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw new DiscordRoleAssignmentConflictException(
                "The Discord role assignments were changed by another request. Refresh and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw new DiscordRoleAssignmentConflictException(
                "The Discord role assignment conflicts with another request.");
        }
    }
}
