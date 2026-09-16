namespace Felion.Application.Discord;

public sealed class DiscordLinkValidationException(string message) : Exception(message);

public sealed class DiscordLinkSubjectNotFoundException(string studentId)
    : Exception($"No active Member or ProbationCandidate exists for StudentId '{studentId}'.");

public sealed class DiscordLinkSubjectAmbiguousException(string studentId)
    : Exception($"StudentId '{studentId}' matches more than one active identity.");

public sealed class DiscordLinkConflictException(string message) : Exception(message);

public sealed class DiscordLinkNotFoundException(Guid memberId)
    : Exception($"No Discord identity link exists for Member '{memberId}'.");
