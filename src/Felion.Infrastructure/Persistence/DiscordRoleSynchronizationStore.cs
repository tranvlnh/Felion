using Felion.Application.Discord;
using Felion.Domain.Audit;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;
using Microsoft.EntityFrameworkCore;

namespace Felion.Infrastructure.Persistence;

internal sealed class DiscordRoleSynchronizationStore(FelionDbContext dbContext)
    : IDiscordRoleSynchronizationStore
{
    public async Task<IReadOnlyList<DiscordRoleMapping>> ListRoleMappingsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.DiscordRoleMappings
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task RecordAuditAsync(
        AuditLog auditLog,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiscordRoleSyncTarget>> ListLinkedActiveTargetsAsync(
        CancellationToken cancellationToken)
    {
        var members = await (
            from link in dbContext.DiscordIdentityLinks.AsNoTracking()
            join member in dbContext.Members.AsNoTracking()
                on link.SubjectId equals member.Id
            where link.SubjectType == DiscordIdentitySubjectType.Member
                && member.Status == MemberStatus.Active
            select new DiscordRoleSyncTarget(
                link.DiscordUserId,
                DiscordIdentitySubjectType.Member,
                member.Position,
                member.DepartmentId,
                member.GenerationId,
                ProbationTeamId: null,
                member.Id))
            .ToListAsync(cancellationToken);

        var candidates = await (
            from link in dbContext.DiscordIdentityLinks.AsNoTracking()
            join candidate in dbContext.ProbationCandidates.AsNoTracking()
                on link.SubjectId equals candidate.Id
            where link.SubjectType == DiscordIdentitySubjectType.Probation
                && candidate.Status == ProbationCandidateStatus.Active
            select new DiscordRoleSyncTarget(
                link.DiscordUserId,
                DiscordIdentitySubjectType.Probation,
                Position: null,
                candidate.DepartmentId,
                candidate.GenerationId,
                candidate.TeamId,
                candidate.Id))
            .ToListAsync(cancellationToken);

        return members.Concat(candidates).ToArray();
    }

    public async Task<DiscordRoleSyncTarget?> FindTargetAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        if (subjectType == DiscordIdentitySubjectType.Member)
        {
            return await (
                from link in dbContext.DiscordIdentityLinks.AsNoTracking()
                join member in dbContext.Members.AsNoTracking()
                    on link.SubjectId equals member.Id
                where link.SubjectType == DiscordIdentitySubjectType.Member
                    && link.SubjectId == subjectId
                    && member.Status == MemberStatus.Active
                select new DiscordRoleSyncTarget(
                    link.DiscordUserId,
                    DiscordIdentitySubjectType.Member,
                    member.Position,
                    member.DepartmentId,
                    member.GenerationId,
                    ProbationTeamId: null,
                    member.Id))
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (subjectType == DiscordIdentitySubjectType.Probation)
        {
            return await (
                from link in dbContext.DiscordIdentityLinks.AsNoTracking()
                join candidate in dbContext.ProbationCandidates.AsNoTracking()
                    on link.SubjectId equals candidate.Id
                where link.SubjectType == DiscordIdentitySubjectType.Probation
                    && link.SubjectId == subjectId
                    && candidate.Status == ProbationCandidateStatus.Active
                select new DiscordRoleSyncTarget(
                    link.DiscordUserId,
                    DiscordIdentitySubjectType.Probation,
                    Position: null,
                    candidate.DepartmentId,
                    candidate.GenerationId,
                    candidate.TeamId,
                    candidate.Id))
                .SingleOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    public Task<long?> FindDiscordUserIdAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        return dbContext.DiscordIdentityLinks
            .AsNoTracking()
            .Where(link => link.SubjectType == subjectType && link.SubjectId == subjectId)
            .Select(link => (long?)link.DiscordUserId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiscordRoleAssignment>> ListRoleAssignmentsAsync(
        DiscordIdentitySubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        return await dbContext.DiscordRoleAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.SubjectType == subjectType && assignment.SubjectId == subjectId)
            .ToListAsync(cancellationToken);
    }
}
