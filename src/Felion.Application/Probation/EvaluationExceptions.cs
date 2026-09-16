namespace Felion.Application.Probation;

public sealed class EvaluationAccessDeniedException()
    : Exception("Only active Core or Admin members may manage evaluations.");

public sealed class EvaluationPeriodNotFoundException(Guid periodId)
    : Exception($"Evaluation period '{periodId}' was not found.");

public sealed class EvaluationFormNotFoundException(Guid formId)
    : Exception($"Evaluation form '{formId}' was not found.");

public sealed class EvaluationValidationException(string message)
    : Exception(message);

public sealed class EvaluationSubmissionValidationException(string message)
    : Exception(message);

public sealed class EvaluationConflictException(string message)
    : Exception(message);
