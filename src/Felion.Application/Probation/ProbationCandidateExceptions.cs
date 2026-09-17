namespace Felion.Application.Probation;

public sealed class ProbationCandidateManagementAccessDeniedException()
    : Exception("Only active Core or Admin members may manage probation candidates.");

public sealed class ProbationCandidateValidationException(string message)
    : Exception(message);

public sealed class ProbationCandidateConflictException(string message)
    : Exception(message);
