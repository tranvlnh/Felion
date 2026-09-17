namespace Felion.Application.Events;

public sealed class EventAccessDeniedException()
    : Exception("Only active members may view events, and only Core or Admin members may manage them.");

public sealed class EventNotFoundException(Guid eventId)
    : Exception($"Event '{eventId}' was not found.");

public sealed class EventPositionNotFoundException(Guid eventId, Guid positionId)
    : Exception($"Position '{positionId}' was not found for event '{eventId}'.");

public sealed class EventRegistrationNotFoundException(Guid eventId, Guid registrationId)
    : Exception($"Registration '{registrationId}' was not found for event '{eventId}'.");

public sealed class EventMemberNotFoundException(Guid memberId)
    : Exception($"Member '{memberId}' was not found.");

public sealed class EventValidationException(string message) : Exception(message);

public sealed class EventConflictException(string message) : Exception(message);
