using System.Text.Json;
using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Probation;

public sealed class ProbationDecisionService(
    IProbationDecisionStore store,
    IMemberStore memberStore,
    IProbationRetentionPolicyProvider retentionPolicyProvider,
    IWorkspaceEmailGenerator emailGenerator,
    IDiscordRoleSynchronizationService? roleSynchronizationService = null) : IProbationDecisionService
{
    public async Task<IReadOnlyList<ProbationDecisionItemResult>> DecideAsync(
        Guid actorMemberId,
        IReadOnlyCollection<ProbationDecisionRequest> requests,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        if (requests.Count == 0)
        {
            throw new ProbationDecisionValidationException("At least one probation decision is required.");
        }

        var duplicateCandidateIds = requests
            .GroupBy(request => request.CandidateId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateCandidateIds.Length > 0)
        {
            throw new ProbationDecisionValidationException(
                "Each probation candidate may appear only once in a bulk decision.");
        }

        var results = new List<ProbationDecisionItemResult>(requests.Count);
        foreach (var request in requests)
        {
            results.Add(await DecideOneAsync(actorMemberId, request, correlationId, cancellationToken));
        }

        return results;
    }

    private async Task<ProbationDecisionItemResult> DecideOneAsync(
        Guid actorMemberId,
        ProbationDecisionRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return request.Decision switch
            {
                ProbationDecision.Pass => await PassAsync(
                    actorMemberId,
                    request.CandidateId,
                    correlationId,
                    cancellationToken),
                ProbationDecision.Fail => await FailAsync(
                    actorMemberId,
                    request.CandidateId,
                    correlationId,
                    cancellationToken),
                _ => throw new ProbationDecisionValidationException("Decision must be Pass or Fail.")
            };
        }
        catch (Exception exception) when (exception is ProbationDecisionNotFoundException
            or ProbationDecisionValidationException
            or ProbationDecisionConflictException)
        {
            return new ProbationDecisionItemResult(
                request.CandidateId,
                request.Decision,
                Succeeded: false,
                exception.Message,
                MemberId: null,
                DiscordRolesSynchronized: false,
                KickQueued: false);
        }
    }

    private async Task<ProbationDecisionItemResult> PassAsync(
        Guid actorMemberId,
        Guid candidateId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var view = await store.FindCandidateAsync(candidateId, track: true, cancellationToken)
            ?? throw new ProbationDecisionNotFoundException(candidateId);
        var candidate = view.Candidate;
        if (candidate.Status != ProbationCandidateStatus.Active)
        {
            throw new ProbationDecisionValidationException("Only an active probation candidate can pass.");
        }

        ValidateIdentityLink(candidate, view.IdentityLink);
        var department = await memberStore.FindDepartmentAsync(candidate.DepartmentId, cancellationToken);
        if (department is null || !department.IsActive)
        {
            throw new ProbationDecisionValidationException(
                "The candidate department no longer exists or is inactive.");
        }

        if (department.IsCore)
        {
            throw new ProbationDecisionValidationException(
                "A probation candidate in the Core department cannot be promoted to a regular Member.");
        }

        var clubEmail = emailGenerator.Generate(candidate.FullName);
        var roleAssignments = await store.ListRoleAssignmentsAsync(
            DiscordIdentitySubjectType.Probation,
            candidate.Id,
            cancellationToken);
        if (await store.MemberStudentIdExistsAsync(candidate.StudentId, cancellationToken))
        {
            throw new ProbationDecisionConflictException("A Member with this StudentId already exists.");
        }

        if (await store.MemberClubEmailExistsAsync(clubEmail, cancellationToken))
        {
            throw new ProbationDecisionConflictException(
                $"The generated Workspace email '{clubEmail}' is already used by another Member.");
        }

        Member member;
        try
        {
            member = Member.Create(
                candidate.StudentId,
                candidate.FullName,
                clubEmail,
                candidate.DepartmentId,
                isCoreDepartment: false,
                candidate.GenerationId,
                MemberPosition.Member);
            candidate.MarkPassed();
            foreach (var assignment in roleAssignments)
            {
                assignment.TransferToMember(member.Id);
            }
        }
        catch (DomainException exception)
        {
            throw new ProbationDecisionValidationException(exception.Message);
        }

        var policy = retentionPolicyProvider.GetPolicy();
        var deleteCandidate = policy.SuccessPolicy == ProbationSuccessPolicy.Delete;
        if (!deleteCandidate)
        {
            candidate.Archive();
        }

        view.IdentityLink?.TransferToMember(member.Id);

        var audit = AuditLog.Create(
            AuditActorType.WebMember,
            actorMemberId,
            actorDiscordUserId: null,
            action: "ProbationCandidatePassed",
            entityType: "ProbationCandidate",
            entityId: candidate.Id,
            correlationId,
            metadataJson: JsonSerializer.Serialize(new
            {
                Decision = ProbationDecision.Pass,
                MemberId = member.Id,
                GeneratedClubEmail = clubEmail,
                DiscordUserId = view.IdentityLink?.DiscordUserId,
                RetentionPolicy = policy.SuccessPolicy
            }),
            beforeJson: Snapshot(candidate),
            afterJson: JsonSerializer.Serialize(new
            {
                candidate.Id,
                candidate.Status,
                MemberId = member.Id,
                member.StudentId,
                member.ClubEmail,
                member.Position,
                member.DepartmentId,
                member.GenerationId
            }));

        await store.SavePassAsync(
            candidate,
            member,
            view.IdentityLink,
            audit,
            deleteCandidate,
            cancellationToken);

        if (view.IdentityLink is not null && roleSynchronizationService is not null)
        {
            await roleSynchronizationService.SynchronizeSubjectAsync(
                DiscordIdentitySubjectType.Member,
                member.Id,
                cancellationToken);
        }

        return new ProbationDecisionItemResult(
            candidateId,
            ProbationDecision.Pass,
            Succeeded: true,
            Error: null,
            member.Id,
            DiscordRolesSynchronized: view.IdentityLink is not null,
            KickQueued: false);
    }

    private async Task<ProbationDecisionItemResult> FailAsync(
        Guid actorMemberId,
        Guid candidateId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var view = await store.FindCandidateAsync(candidateId, track: true, cancellationToken)
            ?? throw new ProbationDecisionNotFoundException(candidateId);
        var candidate = view.Candidate;
        if (candidate.Status != ProbationCandidateStatus.Active)
        {
            throw new ProbationDecisionValidationException("Only an active probation candidate can fail.");
        }

        ValidateIdentityLink(candidate, view.IdentityLink);
        var policy = retentionPolicyProvider.GetPolicy();
        var deleteCandidate = policy.FailurePolicy == ProbationFailurePolicy.Delete;
        try
        {
            candidate.MarkFailed();
        }
        catch (DomainException exception)
        {
            throw new ProbationDecisionValidationException(exception.Message);
        }

        var kickJob = view.IdentityLink is null
            ? null
            : DiscordSyncJob.Create(
                DiscordIdentitySubjectType.Probation,
                candidate.Id,
                DiscordSyncOperation.KickUser,
                JsonSerializer.Serialize(new { DiscordUserId = view.IdentityLink.DiscordUserId }));
        var audit = AuditLog.Create(
            AuditActorType.WebMember,
            actorMemberId,
            actorDiscordUserId: null,
            action: "ProbationCandidateFailed",
            entityType: "ProbationCandidate",
            entityId: candidate.Id,
            correlationId,
            metadataJson: JsonSerializer.Serialize(new
            {
                Decision = ProbationDecision.Fail,
                DiscordUserId = view.IdentityLink?.DiscordUserId,
                RetentionPolicy = policy.FailurePolicy
            }),
            beforeJson: Snapshot(candidate),
            afterJson: JsonSerializer.Serialize(new
            {
                candidate.Id,
                candidate.Status,
                candidate.UpdatedAt
            }));

        await store.SaveFailAsync(
            candidate,
            view.IdentityLink,
            kickJob,
            audit,
            deleteCandidate,
            cancellationToken);

        return new ProbationDecisionItemResult(
            candidateId,
            ProbationDecision.Fail,
            Succeeded: true,
            Error: null,
            MemberId: null,
            DiscordRolesSynchronized: false,
            KickQueued: kickJob is not null);
    }

    private async Task EnsureAuthorizedActorAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position is not (MemberPosition.Admin or MemberPosition.Core))
        {
            throw new ProbationDecisionAccessDeniedException();
        }
    }

    private static void ValidateIdentityLink(
        ProbationCandidate candidate,
        DiscordIdentityLink? identityLink)
    {
        if (identityLink is not null
            && (identityLink.SubjectType != DiscordIdentitySubjectType.Probation
                || identityLink.SubjectId != candidate.Id
                || identityLink.StudentId != candidate.StudentId))
        {
            throw new ProbationDecisionConflictException(
                "The candidate Discord identity link is inconsistent with the candidate record.");
        }
    }

    private static string Snapshot(ProbationCandidate candidate)
    {
        return JsonSerializer.Serialize(new
        {
            candidate.Id,
            candidate.StudentId,
            candidate.FullName,
            candidate.DepartmentId,
            candidate.GenerationId,
            candidate.TeamId,
            candidate.Status,
            candidate.UpdatedAt
        });
    }
}
