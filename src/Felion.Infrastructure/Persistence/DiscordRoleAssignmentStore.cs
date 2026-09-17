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
        var members = await (
            from member in dbContext.Members.AsNoTracking()
            join link in dbContext.DiscordIdentityLinks.AsNoTracking()
                    .Where(link => link.SubjectType == DiscordIdentitySubjectType.Member)
                on member.Id equals link.SubjectId into linkJoin
            from link in linkJoin.DefaultIfEmpty()
            where member.Status == MemberStatus.Active
            select new DiscordRoleAssignmentSubjectView(
                member.Id,
                DiscordIdentitySubjectType.Member,
                member.StudentId,
                member.FullName,
                member.Position,
                member.Status,
                CandidateStatus: null,
                link == null ? null : link.DiscordUserId,
                Assignments: Array.Empty<DiscordRoleAssignment>()))
            .OrderBy(subject => subject.StudentId)
            .ToListAsync(cancellationToken);
        var candidates = await (
            from candidate in dbContext.ProbationCandidates.AsNoTracking()
            join link in dbContext.DiscordIdentityLinks.AsNoTracking()
                    .Where(link => link.SubjectType == DiscordIdentitySubjectType.Probation)
                on candidate.Id equals link.SubjectId into linkJoin
            from link in linkJoin.DefaultIfEmpty()
            where candidate.Status == ProbationCandidateStatus.Active
            select new DiscordRoleAssignmentSubjectView(
                candidate.Id,
                DiscordIdentitySubjectType.Probation,
                candidate.StudentId,
                candidate.FullName,
                MemberPosition: null,
                MemberStatus: null,
                candidate.Status,
                link == null ? null : link.DiscordUserId,
                Assignments: Array.Empty<DiscordRoleAssignment>()))
            .OrderBy(subject => subject.StudentId)
            .ToListAsync(cancellationToken);

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
        DiscordSyncJob? syncJob,
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
            if (syncJob is not null)
            {
                dbContext.DiscordSyncJobs.Add(syncJob);
            }

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
