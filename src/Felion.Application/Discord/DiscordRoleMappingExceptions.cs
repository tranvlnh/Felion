namespace Felion.Application.Discord;

public sealed class DiscordRoleMappingAccessDeniedException()
    : Exception("Only an active Admin member may manage Discord role mappings.");

public sealed class DiscordRoleMappingValidationException(string message) : Exception(message);

public sealed class DiscordRoleMappingConflictException(string message) : Exception(message);

public sealed class DiscordRoleManagementValidationException(string message) : Exception(message);
