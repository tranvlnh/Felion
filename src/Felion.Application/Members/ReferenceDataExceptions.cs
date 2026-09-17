namespace Felion.Application.Members;

public sealed class ReferenceDataAccessDeniedException()
    : Exception("Only an active Admin member may manage Department and Generation reference data.");

public sealed class ReferenceDataValidationException(string message) : Exception(message);

public sealed class ReferenceDataConflictException(string message) : Exception(message);
