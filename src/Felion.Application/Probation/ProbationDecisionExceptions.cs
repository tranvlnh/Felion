namespace Felion.Application.Probation;

public sealed class ProbationDecisionAccessDeniedException()
    : Exception("Only active Core or Admin members may decide probation candidates.");

public sealed class ProbationDecisionNotFoundException(Guid candidateId)
    : Exception($"Probation candidate '{candidateId}' was not found.");

public sealed class ProbationDecisionValidationException(string message)
    : Exception(message);

public sealed class ProbationDecisionConflictException(string message)
    : Exception(message);
