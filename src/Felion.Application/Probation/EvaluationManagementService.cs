using System.Text.Json;
using Felion.Application.Discord;
using Felion.Application.Hardening;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Evaluation;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Probation;

public sealed class EvaluationManagementService(
    IEvaluationStore store,
    IMemberStore memberStore,
    IProbationTeamStore teamStore,
    IProbationCandidateStore candidateStore,
    IDiscordLinkStore linkStore,
    IRateLimitGate rateLimitGate) : IEvaluationManagementService
{
    public async Task<EvaluationPeriodDto> CreatePeriodAsync(
        Guid actorMemberId,
        CreateEvaluationPeriodCommand command,
        string correlationId,
        long? actorDiscordUserId,
        CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(actorMemberId, cancellationToken);
        EvaluationPeriod period;
        try
        {
            period = EvaluationPeriod.Create(command.Name);
        }
        catch (DomainException exception)
        {
            throw new EvaluationValidationException(exception.Message);
        }

        await store.AddPeriodAsync(
            period,
            CreateAudit(actorMemberId, actorDiscordUserId, "EvaluationPeriodCreated", period.Id, correlationId, after: Snapshot(period)),
            cancellationToken);
        return ToDto(period);
    }

    public async Task<EvaluationPeriodDto> ClosePeriodAsync(
        Guid actorMemberId,
        Guid periodId,
        string correlationId,
        long? actorDiscordUserId,
        CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(actorMemberId, cancellationToken);
        var period = await store.FindPeriodAsync(periodId, track: true, cancellationToken)
            ?? throw new EvaluationPeriodNotFoundException(periodId);
        var before = Snapshot(period);
        try
        {
            period.Close();
        }
        catch (DomainException exception)
        {
            throw new EvaluationValidationException(exception.Message);
        }

        await store.ClosePeriodAsync(
            period,
            CreateAudit(
                actorMemberId,
                actorDiscordUserId,
                "EvaluationPeriodClosed",
                period.Id,
                correlationId,
                before,
                Snapshot(period)),
            cancellationToken);
        return ToDto(period);
    }

    public async Task<IReadOnlyList<EvaluationPeriodDto>> ListPeriodsAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        await EnsureCoreOrAdminAsync(actorMemberId, cancellationToken);
        return (await store.ListPeriodsAsync(cancellationToken))
            .Select(ToDto)
            .ToArray();
    }

    public async Task<EvaluationPeriodDto?> GetCurrentPeriodAsync(CancellationToken cancellationToken)
    {
        var period = (await store.ListPeriodsAsync(cancellationToken))
            .Where(candidate => candidate.Status == EvaluationPeriodStatus.Open)
            .OrderByDescending(candidate => candidate.OpenedAt)
            .FirstOrDefault();
        return period is null ? null : ToDto(period);
    }

    public async Task<EvaluationStatusDto?> GetCurrentStatusAsync(CancellationToken cancellationToken)
    {
        var period = (await store.ListPeriodsAsync(cancellationToken))
            .Where(candidate => candidate.Status == EvaluationPeriodStatus.Open)
            .OrderByDescending(candidate => candidate.OpenedAt)
            .FirstOrDefault();
        return period is null
            ? null
            : new EvaluationStatusDto(ToDto(period), await BuildProgressAsync(period.Id, cancellationToken));
    }

    public async Task<EvaluationStatusDto> GetStatusAsync(
        Guid actorMemberId,
        Guid? periodId,
        CancellationToken cancellationToken)
    {
        await EnsureCoreOrAdminAsync(actorMemberId, cancellationToken);
        var period = await ResolvePeriodForReadAsync(periodId, cancellationToken);
        var progress = await BuildProgressAsync(period.Id, cancellationToken);
        return new EvaluationStatusDto(ToDto(period), progress);
    }

    public async Task<EvaluationStatusDto> GetStatusByNameAsync(
        Guid actorMemberId,
        string periodName,
        CancellationToken cancellationToken)
    {
        await EnsureCoreOrAdminAsync(actorMemberId, cancellationToken);
        var periodId = await ResolvePeriodIdByNameAsync(periodName, cancellationToken);
        return await GetStatusAsync(actorMemberId, periodId, cancellationToken);
    }

    public async Task<EvaluationTargetListDto> GetPeerTargetsAsync(
        long discordUserId,
        Guid? periodId,
        CancellationToken cancellationToken)
    {
        var reviewer = await RequireCandidateIdentityAsync(discordUserId, cancellationToken);
        var period = await ResolveOpenPeriodAsync(periodId, cancellationToken);
        if (reviewer.TeamId is null)
        {
            throw new EvaluationParticipantAccessDeniedException(
                "Bạn phải thuộc một probation team để đánh giá peer.");
        }

        var team = await teamStore.FindViewAsync(reviewer.TeamId.Value, cancellationToken)
            ?? throw new EvaluationParticipantAccessDeniedException("Probation team của bạn không tồn tại.");
        var targets = new List<EvaluationTargetDto>();
        foreach (var candidate in await LoadActiveCandidatesAsync(team, cancellationToken))
        {
            if (candidate.Id == reviewer.Id)
            {
                continue;
            }

            var existing = await store.FindPeerEvaluationAsync(
                period.Id,
                reviewer.Id,
                candidate.Id,
                track: false,
                cancellationToken);
            targets.Add(new EvaluationTargetDto(
                candidate.Id,
                candidate.StudentId,
                candidate.FullName,
                existing is not null));
        }

        return new EvaluationTargetListDto(
            ToDto(period),
            [new EvaluationTeamTargetDto(team.Id, team.Name, targets)]);
    }

    public async Task<EvaluationTargetListDto> GetMentorTargetsAsync(
        long discordUserId,
        Guid? periodId,
        CancellationToken cancellationToken)
    {
        var mentor = await RequireMemberIdentityAsync(discordUserId, cancellationToken);
        var period = await ResolveOpenPeriodAsync(periodId, cancellationToken);
        var teams = await teamStore.ListAsync(cancellationToken);
        var result = new List<EvaluationTeamTargetDto>();
        foreach (var team in teams.Where(team => team.MentorMemberIds.Contains(mentor.Id)))
        {
            var targets = new List<EvaluationTargetDto>();
            foreach (var candidate in await LoadActiveCandidatesAsync(team, cancellationToken))
            {
                var existing = await store.FindMentorEvaluationAsync(
                    period.Id,
                    mentor.Id,
                    candidate.Id,
                    track: false,
                    cancellationToken);
                targets.Add(new EvaluationTargetDto(
                    candidate.Id,
                    candidate.StudentId,
                    candidate.FullName,
                    existing is not null));
            }

            result.Add(new EvaluationTeamTargetDto(team.Id, team.Name, targets));
        }

        if (result.Count == 0)
        {
            throw new EvaluationParticipantAccessDeniedException(
                "Bạn chưa được gán làm mentor của probation team nào.");
        }

        return new EvaluationTargetListDto(ToDto(period), result);
    }

    public async Task<PeerEvaluationSubmissionDto?> GetPeerEvaluationAsync(
        long discordUserId,
        Guid periodId,
        Guid targetCandidateId,
        CancellationToken cancellationToken)
    {
        var reviewer = await RequireCandidateIdentityAsync(discordUserId, cancellationToken);
        var period = await ResolveOpenPeriodAsync(periodId, cancellationToken);
        await EnsurePeerTargetAsync(reviewer, targetCandidateId, cancellationToken);
        var evaluation = await store.FindPeerEvaluationAsync(
            period.Id,
            reviewer.Id,
            targetCandidateId,
            track: false,
            cancellationToken);
        return evaluation is null ? null : ToDto(evaluation);
    }

    public async Task<MentorEvaluationSubmissionDto?> GetMentorEvaluationAsync(
        long discordUserId,
        Guid periodId,
        Guid targetCandidateId,
        CancellationToken cancellationToken)
    {
        var mentor = await RequireMemberIdentityAsync(discordUserId, cancellationToken);
        var period = await ResolveOpenPeriodAsync(periodId, cancellationToken);
        await EnsureMentorTargetAsync(mentor, targetCandidateId, cancellationToken);
        var evaluation = await store.FindMentorEvaluationAsync(
            period.Id,
            mentor.Id,
            targetCandidateId,
            track: false,
            cancellationToken);
        return evaluation is null ? null : ToDto(evaluation);
    }

    public async Task<EvaluationSubmissionReceiptDto> SubmitPeerEvaluationAsync(
        long discordUserId,
        SubmitPeerEvaluationCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureRateLimitAsync("Peer", discordUserId, cancellationToken);
        var reviewer = await RequireCandidateIdentityAsync(discordUserId, cancellationToken);
        var period = await ResolveOpenPeriodAsync(command.PeriodId, cancellationToken);
        var target = await EnsurePeerTargetAsync(reviewer, command.TargetCandidateId, cancellationToken);
        var existing = await store.FindPeerEvaluationAsync(
            period.Id,
            reviewer.Id,
            target.Id,
            track: true,
            cancellationToken);
        var wasUpdate = existing is not null;
        PeerEvaluation evaluation;
        try
        {
            if (existing is null)
            {
                evaluation = PeerEvaluation.Create(
                    period.Id,
                    reviewer.Id,
                    target.Id,
                    reviewer.StudentId,
                    reviewer.FullName,
                    target.StudentId,
                    target.FullName,
                    command.Contribution,
                    command.Communication,
                    command.Attitude,
                    command.Note);
            }
            else
            {
                evaluation = existing;
                evaluation.Update(
                    command.Contribution,
                    command.Communication,
                    command.Attitude,
                    command.Note);
            }
        }
        catch (DomainException exception)
        {
            throw new EvaluationSubmissionValidationException(exception.Message);
        }

        await store.SavePeerEvaluationAsync(
            evaluation,
            CreateEvaluationAudit(
                discordUserId,
                wasUpdate ? "PeerEvaluationUpdated" : "PeerEvaluationSubmitted",
                evaluation.Id,
                correlationId,
                period.Id,
                target.Id,
                evaluation.Contribution,
                evaluation.Communication,
                evaluation.Attitude),
            isNew: !wasUpdate,
            cancellationToken);
        return new EvaluationSubmissionReceiptDto(evaluation.Id, wasUpdate, evaluation.CreatedAt, evaluation.UpdatedAt);
    }

    public async Task<EvaluationSubmissionReceiptDto> SubmitMentorEvaluationAsync(
        long discordUserId,
        SubmitMentorEvaluationCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureRateLimitAsync("Mentor", discordUserId, cancellationToken);
        var mentor = await RequireMemberIdentityAsync(discordUserId, cancellationToken);
        var period = await ResolveOpenPeriodAsync(command.PeriodId, cancellationToken);
        var target = await EnsureMentorTargetAsync(mentor, command.TargetCandidateId, cancellationToken);
        var existing = await store.FindMentorEvaluationAsync(
            period.Id,
            mentor.Id,
            target.Id,
            track: true,
            cancellationToken);
        var wasUpdate = existing is not null;
        MentorEvaluation evaluation;
        try
        {
            if (existing is null)
            {
                evaluation = MentorEvaluation.Create(
                    period.Id,
                    mentor.Id,
                    target.Id,
                    mentor.StudentId,
                    mentor.FullName,
                    target.StudentId,
                    target.FullName,
                    command.Attendance,
                    command.TaskCompletion,
                    command.LearningInitiative,
                    command.Note);
            }
            else
            {
                evaluation = existing;
                evaluation.Update(
                    command.Attendance,
                    command.TaskCompletion,
                    command.LearningInitiative,
                    command.Note);
            }
        }
        catch (DomainException exception)
        {
            throw new EvaluationSubmissionValidationException(exception.Message);
        }

        await store.SaveMentorEvaluationAsync(
            evaluation,
            CreateEvaluationAudit(
                discordUserId,
                wasUpdate ? "MentorEvaluationUpdated" : "MentorEvaluationSubmitted",
                evaluation.Id,
                correlationId,
                period.Id,
                target.Id,
                evaluation.Attendance,
                evaluation.TaskCompletion,
                evaluation.LearningInitiative),
            isNew: !wasUpdate,
            cancellationToken);
        return new EvaluationSubmissionReceiptDto(evaluation.Id, wasUpdate, evaluation.CreatedAt, evaluation.UpdatedAt);
    }

    public async Task<EvaluationDetailDto> ViewAsync(
        Guid actorMemberId,
        Guid candidateId,
        Guid? periodId,
        CancellationToken cancellationToken)
    {
        await EnsureCoreOrAdminAsync(actorMemberId, cancellationToken);
        var period = await ResolvePeriodForReadAsync(periodId, cancellationToken);
        var peer = await store.ListPeerEvaluationsAsync(period.Id, candidateId, cancellationToken);
        var mentor = await store.ListMentorEvaluationsAsync(period.Id, candidateId, cancellationToken);
        var candidateView = await candidateStore.FindViewAsync(candidateId, cancellationToken);
        if (candidateView is null && peer.Count == 0 && mentor.Count == 0)
        {
            throw new ProbationCandidateNotFoundException(candidateId);
        }

        var candidateStudentId = candidateView?.Candidate.StudentId
            ?? peer.Select(evaluation => evaluation.TargetStudentIdSnapshot)
                .Concat(mentor.Select(evaluation => evaluation.TargetStudentIdSnapshot))
                .FirstOrDefault()
            ?? string.Empty;
        var candidateName = candidateView?.Candidate.FullName
            ?? peer.Select(evaluation => evaluation.TargetNameSnapshot)
                .Concat(mentor.Select(evaluation => evaluation.TargetNameSnapshot))
                .FirstOrDefault()
            ?? "Archived or deleted candidate";
        var team = candidateView?.Team;
        var summary = new EvaluationCandidateSummaryDto(
            candidateId,
            candidateStudentId,
            candidateName,
            team?.Id ?? Guid.Empty,
            team?.Name ?? "Archived or deleted team",
            AggregatePeer(peer),
            AggregateMentor(mentor));
        return new EvaluationDetailDto(
            ToDto(period),
            summary,
            peer.Select(ToDto).ToArray(),
            mentor.Select(ToDto).ToArray());
    }

    public async Task<EvaluationDetailDto> ViewByPeriodNameAsync(
        Guid actorMemberId,
        Guid candidateId,
        string? periodName,
        CancellationToken cancellationToken)
    {
        await EnsureCoreOrAdminAsync(actorMemberId, cancellationToken);
        var periodId = string.IsNullOrWhiteSpace(periodName)
            ? (Guid?)null
            : await ResolvePeriodIdByNameAsync(periodName, cancellationToken);
        return await ViewAsync(actorMemberId, candidateId, periodId, cancellationToken);
    }

    public async Task<EvaluationSummaryDto> SummaryAsync(
        Guid actorMemberId,
        Guid periodId,
        Guid? teamId,
        CancellationToken cancellationToken)
    {
        await EnsureCoreOrAdminAsync(actorMemberId, cancellationToken);
        var period = await ResolvePeriodForReadAsync(periodId, cancellationToken);
        var teams = await LoadActiveTeamsAsync(cancellationToken);
        if (teamId is not null)
        {
            teams = teams.Where(team => team.Id == teamId.Value).ToArray();
            if (teams.Count == 0)
            {
                throw new ProbationTeamNotFoundException(teamId.Value);
            }
        }

        var peer = await store.ListPeerEvaluationsAsync(period.Id, null, cancellationToken);
        var mentor = await store.ListMentorEvaluationsAsync(period.Id, null, cancellationToken);
        var candidates = teams
            .SelectMany(team => team.Candidates.Select(candidate => new EvaluationCandidateSummaryDto(
                candidate.Id,
                candidate.StudentId,
                candidate.FullName,
                team.Id,
                team.Name,
                AggregatePeer(peer.Where(evaluation => evaluation.TargetCandidateId == candidate.Id)),
                AggregateMentor(mentor.Where(evaluation => evaluation.TargetCandidateId == candidate.Id)))))
            .ToList();
        var knownCandidateIds = candidates.Select(candidate => candidate.CandidateId).ToHashSet();
        var historicalTargetIds = peer.Select(evaluation => evaluation.TargetCandidateId)
            .Concat(mentor.Select(evaluation => evaluation.TargetCandidateId))
            .Distinct();
        foreach (var targetId in historicalTargetIds)
        {
            if (knownCandidateIds.Contains(targetId) || teamId is not null)
            {
                continue;
            }

            var peerSnapshot = peer.FirstOrDefault(item => item.TargetCandidateId == targetId);
            var mentorSnapshot = mentor.FirstOrDefault(item => item.TargetCandidateId == targetId);
            candidates.Add(new EvaluationCandidateSummaryDto(
                targetId,
                peerSnapshot?.TargetStudentIdSnapshot ?? mentorSnapshot!.TargetStudentIdSnapshot,
                peerSnapshot?.TargetNameSnapshot ?? mentorSnapshot!.TargetNameSnapshot,
                Guid.Empty,
                "Archived or deleted team",
                AggregatePeer(peer.Where(item => item.TargetCandidateId == targetId)),
                AggregateMentor(mentor.Where(item => item.TargetCandidateId == targetId))));
        }
        var progress = await BuildProgressAsync(period.Id, cancellationToken, teams);
        return new EvaluationSummaryDto(
            ToDto(period),
            teamId is null ? null : teams.Single().Name,
            candidates,
            progress);
    }

    public async Task<EvaluationSummaryDto> SummaryByNameAsync(
        Guid actorMemberId,
        string periodName,
        string? teamName,
        CancellationToken cancellationToken)
    {
        await EnsureCoreOrAdminAsync(actorMemberId, cancellationToken);
        var periodId = await ResolvePeriodIdByNameAsync(periodName, cancellationToken);
        var teamId = string.IsNullOrWhiteSpace(teamName)
            ? (Guid?)null
            : await ResolveActiveTeamIdByNameAsync(teamName, cancellationToken);
        return await SummaryAsync(actorMemberId, periodId, teamId, cancellationToken);
    }

    private async Task<EvaluationProgressDto> BuildProgressAsync(
        Guid periodId,
        CancellationToken cancellationToken,
        IReadOnlyList<TeamParticipants>? selectedTeams = null)
    {
        var teams = selectedTeams ?? await LoadActiveTeamsAsync(cancellationToken);
        var peer = await store.ListPeerEvaluationsAsync(periodId, null, cancellationToken);
        var mentor = await store.ListMentorEvaluationsAsync(periodId, null, cancellationToken);
        var peerKeys = teams
            .SelectMany(team => team.Candidates.SelectMany(evaluator => team.Candidates
                .Where(target => target.Id != evaluator.Id)
                .Select(target => (EvaluatorId: evaluator.Id, TargetId: target.Id, Team: team))))
            .ToArray();
        var mentorKeys = teams
            .SelectMany(team => team.Mentors.SelectMany(reviewer => team.Candidates
                .Select(target => (ReviewerId: reviewer.Id, TargetId: target.Id, Team: team))))
            .ToArray();
        var peerSet = peer.Select(evaluation => (evaluation.EvaluatorCandidateId, evaluation.TargetCandidateId)).ToHashSet();
        var mentorSet = mentor.Select(evaluation => (evaluation.MentorMemberId, evaluation.TargetCandidateId)).ToHashSet();
        var teamProgress = teams.Select(team =>
        {
            var teamPeer = peerKeys.Where(key => key.Team.Id == team.Id).ToArray();
            var teamMentor = mentorKeys.Where(key => key.Team.Id == team.Id).ToArray();
            return new EvaluationTeamProgressDto(
                team.Id,
                team.Name,
                teamPeer.Count(key => peerSet.Contains((key.EvaluatorId, key.TargetId))),
                teamPeer.Length,
                teamMentor.Count(key => mentorSet.Contains((key.ReviewerId, key.TargetId))),
                teamMentor.Length);
        }).ToArray();
        var missingPeer = peerKeys
            .Where(key => !peerSet.Contains((key.EvaluatorId, key.TargetId)))
            .Select(key => new MissingPeerEvaluationDto(
                key.Team.Candidates.Single(candidate => candidate.Id == key.EvaluatorId).StudentId,
                key.Team.Candidates.Single(candidate => candidate.Id == key.EvaluatorId).FullName,
                key.Team.Candidates.Single(candidate => candidate.Id == key.TargetId).StudentId,
                key.Team.Candidates.Single(candidate => candidate.Id == key.TargetId).FullName,
                key.Team.Id,
                key.Team.Name))
            .ToArray();
        var missingMentor = mentorKeys
            .Where(key => !mentorSet.Contains((key.ReviewerId, key.TargetId)))
            .Select(key => new MissingMentorEvaluationDto(
                key.Team.Mentors.Single(mentor => mentor.Id == key.ReviewerId).StudentId,
                key.Team.Mentors.Single(mentor => mentor.Id == key.ReviewerId).FullName,
                key.Team.Candidates.Single(candidate => candidate.Id == key.TargetId).StudentId,
                key.Team.Candidates.Single(candidate => candidate.Id == key.TargetId).FullName,
                key.Team.Id,
                key.Team.Name))
            .ToArray();
        return new EvaluationProgressDto(
            teamProgress.Sum(team => team.PeerSubmitted),
            teamProgress.Sum(team => team.PeerExpected),
            teamProgress.Sum(team => team.MentorSubmitted),
            teamProgress.Sum(team => team.MentorExpected),
            teamProgress,
            missingPeer,
            missingMentor);
    }

    private async Task<IReadOnlyList<TeamParticipants>> LoadActiveTeamsAsync(CancellationToken cancellationToken)
    {
        var teams = await teamStore.ListAsync(cancellationToken);
        var result = new List<TeamParticipants>(teams.Count);
        foreach (var team in teams)
        {
            var candidates = await LoadActiveCandidatesAsync(team, cancellationToken);
            var mentors = new List<ParticipantMember>();
            foreach (var mentorId in team.MentorMemberIds)
            {
                var mentor = await memberStore.FindByIdAsync(mentorId, track: false, cancellationToken);
                if (mentor?.Status == MemberStatus.Active)
                {
                    mentors.Add(new ParticipantMember(mentor.Id, mentor.StudentId, mentor.FullName));
                }
            }

            result.Add(new TeamParticipants(team.Id, team.Name, candidates, mentors));
        }

        return result;
    }

    private async Task<IReadOnlyList<ParticipantCandidate>> LoadActiveCandidatesAsync(
        ProbationTeamView team,
        CancellationToken cancellationToken)
    {
        var candidates = new List<ParticipantCandidate>();
        foreach (var candidateSummary in team.Candidates ?? [])
        {
            var candidate = await teamStore.FindCandidateAsync(
                candidateSummary.Id,
                track: false,
                cancellationToken);
            if (candidate?.Status == ProbationCandidateStatus.Active)
            {
                candidates.Add(new ParticipantCandidate(candidate.Id, candidate.StudentId, candidate.FullName));
            }
        }

        return candidates;
    }

    private async Task<ProbationCandidate> EnsurePeerTargetAsync(
        ProbationCandidate reviewer,
        Guid targetCandidateId,
        CancellationToken cancellationToken)
    {
        var target = await teamStore.FindCandidateAsync(targetCandidateId, track: false, cancellationToken)
            ?? throw new ProbationCandidateNotFoundException(targetCandidateId);
        if (target.Status != ProbationCandidateStatus.Active)
        {
            throw new EvaluationSubmissionValidationException("Chỉ candidate đang active mới được đánh giá.");
        }

        if (reviewer.Id == target.Id)
        {
            throw new EvaluationSubmissionValidationException("Bạn không được tự đánh giá chính mình.");
        }

        if (reviewer.TeamId is null || reviewer.TeamId != target.TeamId)
        {
            throw new EvaluationSubmissionValidationException(
                "Peer evaluation chỉ được thực hiện giữa candidate cùng team.");
        }

        return target;
    }

    private async Task<ProbationCandidate> EnsureMentorTargetAsync(
        Member mentor,
        Guid targetCandidateId,
        CancellationToken cancellationToken)
    {
        var target = await teamStore.FindCandidateAsync(targetCandidateId, track: false, cancellationToken)
            ?? throw new ProbationCandidateNotFoundException(targetCandidateId);
        if (target.Status != ProbationCandidateStatus.Active)
        {
            throw new EvaluationSubmissionValidationException("Chỉ candidate đang active mới được đánh giá.");
        }

        if (target.TeamId is null
            || await teamStore.FindMentorAsync(target.TeamId.Value, mentor.Id, track: false, cancellationToken) is null)
        {
            throw new EvaluationSubmissionValidationException(
                "Mentor chỉ được đánh giá candidate thuộc team mình mentor.");
        }

        return target;
    }

    private async Task<ProbationCandidate> RequireCandidateIdentityAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        var link = await RequireIdentityLinkAsync(discordUserId, cancellationToken);
        if (link.SubjectType != DiscordIdentitySubjectType.Probation)
        {
            throw new EvaluationParticipantAccessDeniedException(
                "Discord account này không được liên kết với probation candidate.");
        }

        var candidate = await teamStore.FindCandidateAsync(link.SubjectId, track: false, cancellationToken);
        if (candidate?.Status != ProbationCandidateStatus.Active)
        {
            throw new EvaluationParticipantAccessDeniedException(
                "Chỉ probation candidate đang active mới được peer evaluation.");
        }

        return candidate;
    }

    private async Task<Member> RequireMemberIdentityAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        var link = await RequireIdentityLinkAsync(discordUserId, cancellationToken);
        if (link.SubjectType != DiscordIdentitySubjectType.Member)
        {
            throw new EvaluationParticipantAccessDeniedException(
                "Discord account này không được liên kết với active Member mentor.");
        }

        var member = await memberStore.FindByIdAsync(link.SubjectId, track: false, cancellationToken);
        if (member?.Status != MemberStatus.Active)
        {
            throw new EvaluationParticipantAccessDeniedException(
                "Chỉ Member đang active mới được mentor evaluation.");
        }

        return member;
    }

    private async Task<DiscordIdentityLink> RequireIdentityLinkAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        if (discordUserId <= 0)
        {
            throw new EvaluationParticipantAccessDeniedException("Discord user ID không hợp lệ.");
        }

        return await linkStore.FindByDiscordUserIdAsync(discordUserId, cancellationToken)
            ?? throw new EvaluationParticipantAccessDeniedException(
                "Bạn chưa liên kết Discord account với Felion.");
    }

    private async Task EnsureRateLimitAsync(
        string reviewerType,
        long discordUserId,
        CancellationToken cancellationToken)
    {
        var rateLimit = await rateLimitGate.TryAcquireAsync(
            RateLimitOperation.EvaluationSubmission,
            $"{reviewerType}:{discordUserId}",
            cancellationToken);
        if (!rateLimit.IsAcquired)
        {
            throw new RateLimitExceededException(RateLimitOperation.EvaluationSubmission, rateLimit.RetryAfter);
        }
    }

    private async Task<EvaluationPeriod> ResolveOpenPeriodAsync(
        Guid? periodId,
        CancellationToken cancellationToken)
    {
        var period = await ResolvePeriodForReadAsync(periodId, cancellationToken);
        if (period.Status != EvaluationPeriodStatus.Open)
        {
            throw new EvaluationSubmissionValidationException(
                "Period đã đóng và không nhận submission mới hoặc chỉnh sửa.");
        }

        return period;
    }

    private async Task<EvaluationPeriod> ResolvePeriodForReadAsync(
        Guid? periodId,
        CancellationToken cancellationToken)
    {
        if (periodId is not null)
        {
            return await store.FindPeriodAsync(periodId.Value, track: false, cancellationToken)
                ?? throw new EvaluationPeriodNotFoundException(periodId.Value);
        }

        var period = (await store.ListPeriodsAsync(cancellationToken))
            .OrderByDescending(candidate => candidate.Status == EvaluationPeriodStatus.Open)
            .ThenByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefault();
        return period ?? throw new EvaluationPeriodRequiredException();
    }

    private async Task<Guid> ResolvePeriodIdByNameAsync(
        string periodName,
        CancellationToken cancellationToken)
    {
        var normalizedName = periodName.Trim();
        if (normalizedName.Length == 0)
        {
            throw new EvaluationPeriodNameNotFoundException(periodName);
        }

        var matches = (await store.ListPeriodsAsync(cancellationToken))
            .Where(period => string.Equals(period.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0].Id,
            0 => throw new EvaluationPeriodNameNotFoundException(normalizedName),
            _ => throw new EvaluationPeriodNameAmbiguousException(normalizedName)
        };
    }

    private async Task<Guid> ResolveActiveTeamIdByNameAsync(
        string teamName,
        CancellationToken cancellationToken)
    {
        var normalizedName = teamName.Trim();
        if (normalizedName.Length == 0)
        {
            throw new ProbationTeamNameNotFoundException(teamName);
        }

        var matches = (await teamStore.ListAsync(cancellationToken))
            .Where(team => team.IsActive
                && string.Equals(team.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0].Id,
            0 => throw new ProbationTeamNameNotFoundException(normalizedName),
            _ => throw new ProbationTeamNameAmbiguousException(normalizedName)
        };
    }

    private async Task EnsureAdminAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor?.Status != MemberStatus.Active || actor.Position != MemberPosition.Admin)
        {
            throw new EvaluationAdminAccessDeniedException();
        }
    }

    private async Task EnsureCoreOrAdminAsync(Guid actorMemberId, CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor?.Status != MemberStatus.Active
            || actor.Position is not (MemberPosition.Admin or MemberPosition.Core))
        {
            throw new EvaluationAccessDeniedException();
        }
    }

    private static PeerEvaluationAggregateDto AggregatePeer(IEnumerable<PeerEvaluation> evaluations)
    {
        var values = evaluations.ToArray();
        return values.Length == 0
            ? new PeerEvaluationAggregateDto(null, null, null, 0)
            : new PeerEvaluationAggregateDto(
                Average(values.Select(value => value.Contribution)),
                Average(values.Select(value => value.Communication)),
                Average(values.Select(value => value.Attitude)),
                values.Length);
    }

    private static MentorEvaluationAggregateDto AggregateMentor(IEnumerable<MentorEvaluation> evaluations)
    {
        var values = evaluations.ToArray();
        return values.Length == 0
            ? new MentorEvaluationAggregateDto(null, null, null, 0)
            : new MentorEvaluationAggregateDto(
                Average(values.Select(value => value.Attendance)),
                Average(values.Select(value => value.TaskCompletion)),
                Average(values.Select(value => value.LearningInitiative)),
                values.Length);
    }

    private static decimal Average(IEnumerable<int> values)
    {
        return decimal.Round(values.Select(value => (decimal)value).Average(), 2);
    }

    private static AuditLog CreateAudit(
        Guid actorMemberId,
        long? actorDiscordUserId,
        string action,
        Guid entityId,
        string correlationId,
        string? before = null,
        string? after = null)
    {
        return AuditLog.Create(
            actorDiscordUserId is null ? AuditActorType.WebMember : AuditActorType.DiscordMember,
            actorDiscordUserId is null ? actorMemberId : null,
            actorDiscordUserId,
            action,
            "EvaluationPeriod",
            entityId,
            correlationId,
            beforeJson: before,
            afterJson: after);
    }

    private static AuditLog CreateEvaluationAudit(
        long actorDiscordUserId,
        string action,
        Guid entityId,
        string correlationId,
        Guid periodId,
        Guid targetCandidateId,
        int firstScore,
        int secondScore,
        int thirdScore)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            PeriodId = periodId,
            TargetCandidateId = targetCandidateId,
            Scores = new[] { firstScore, secondScore, thirdScore }
        });
        return AuditLog.Create(
            AuditActorType.DiscordMember,
            actorMemberId: null,
            actorDiscordUserId: actorDiscordUserId,
            action,
            action.StartsWith("Peer", StringComparison.Ordinal) ? "PeerEvaluation" : "MentorEvaluation",
            entityId,
            correlationId,
            metadataJson: metadata);
    }

    private static string Snapshot(EvaluationPeriod period)
    {
        return JsonSerializer.Serialize(new
        {
            period.Id,
            period.Name,
            period.Status,
            period.CreatedAt,
            period.OpenedAt,
            period.ClosedAt
        });
    }

    private static EvaluationPeriodDto ToDto(EvaluationPeriod period)
    {
        return new EvaluationPeriodDto(
            period.Id,
            period.Name,
            period.CreatedAt,
            period.OpenedAt,
            period.ClosedAt,
            period.Status);
    }

    private static PeerEvaluationSubmissionDto ToDto(PeerEvaluation evaluation)
    {
        return new PeerEvaluationSubmissionDto(
            evaluation.Id,
            evaluation.EvaluatorCandidateId,
            evaluation.EvaluatorStudentIdSnapshot,
            evaluation.EvaluatorNameSnapshot,
            evaluation.TargetCandidateId,
            evaluation.TargetStudentIdSnapshot,
            evaluation.TargetNameSnapshot,
            evaluation.Contribution,
            evaluation.Communication,
            evaluation.Attitude,
            evaluation.Note,
            evaluation.CreatedAt,
            evaluation.UpdatedAt);
    }

    private static MentorEvaluationSubmissionDto ToDto(MentorEvaluation evaluation)
    {
        return new MentorEvaluationSubmissionDto(
            evaluation.Id,
            evaluation.MentorMemberId,
            evaluation.MentorStudentIdSnapshot,
            evaluation.MentorNameSnapshot,
            evaluation.TargetCandidateId,
            evaluation.TargetStudentIdSnapshot,
            evaluation.TargetNameSnapshot,
            evaluation.Attendance,
            evaluation.TaskCompletion,
            evaluation.LearningInitiative,
            evaluation.Note,
            evaluation.CreatedAt,
            evaluation.UpdatedAt);
    }

    private sealed record ParticipantCandidate(Guid Id, string StudentId, string FullName);

    private sealed record ParticipantMember(Guid Id, string StudentId, string FullName);

    private sealed record TeamParticipants(
        Guid Id,
        string Name,
        IReadOnlyList<ParticipantCandidate> Candidates,
        IReadOnlyList<ParticipantMember> Mentors);
}
