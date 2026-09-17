namespace Felion.Application.Probation;

public sealed class EvaluationAccessDeniedException()
    : Exception("Only active Core or Admin members may access evaluation administration.");

public sealed class EvaluationAdminAccessDeniedException()
    : Exception("Only an active Admin may create, open or close evaluation periods.");

public sealed class EvaluationParticipantAccessDeniedException(string message)
    : Exception(message);

public sealed class EvaluationPeriodNotFoundException(Guid periodId)
    : Exception($"Evaluation period '{periodId}' was not found.");

public sealed class EvaluationPeriodNameNotFoundException(string periodName)
    : Exception($"Evaluation period '{periodName}' was not found.");

public sealed class EvaluationPeriodNameAmbiguousException(string periodName)
    : Exception($"Multiple evaluation periods match '{periodName}'. Use a unique period name.");

public sealed class ProbationTeamNameNotFoundException(string teamName)
    : Exception($"Probation team '{teamName}' was not found.");

public sealed class ProbationTeamNameAmbiguousException(string teamName)
    : Exception($"Multiple active probation teams match '{teamName}'. Use a unique team name.");

public sealed class EvaluationValidationException(string message)
    : Exception(message);

public sealed class EvaluationSubmissionValidationException(string message)
    : Exception(message);

public sealed class EvaluationConflictException(string message)
    : Exception(message);

public sealed class EvaluationPeriodRequiredException()
    : Exception("There is no evaluation period available for this operation.");
