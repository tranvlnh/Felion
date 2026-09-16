namespace Felion.Application.Probation;

public sealed class ProbationTeamAccessDeniedException()
    : Exception("Only active Core or Admin members may manage probation teams.");

public sealed class ProbationTeamNotFoundException(Guid teamId)
    : Exception($"Probation team '{teamId}' was not found.");

public sealed class ProbationCandidateNotFoundException(Guid candidateId)
    : Exception($"Probation candidate '{candidateId}' was not found.");

public sealed class ProbationMentorNotFoundException(Guid teamId, Guid memberId)
    : Exception($"Mentor '{memberId}' is not assigned to probation team '{teamId}'.");

public sealed class ProbationTeamValidationException(string message)
    : Exception(message);

public sealed class ProbationTeamConflictException(string message)
    : Exception(message);
