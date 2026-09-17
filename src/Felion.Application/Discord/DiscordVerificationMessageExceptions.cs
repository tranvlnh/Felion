namespace Felion.Application.Discord;

public sealed class DiscordVerificationMessageAccessDeniedException()
    : Exception("Only an active linked Admin or a Discord server Administrator may publish the verification message.");

public sealed class DiscordVerificationMessageValidationException(string message) : Exception(message);

public class DiscordVerificationMessageGatewayException(string message) : Exception(message);

public sealed class DiscordVerificationMessageUnavailableException()
    : DiscordVerificationMessageGatewayException(
        "Discord verification message publishing is unavailable because the Discord gateway is not configured.");

public sealed class DiscordGuildPermissionGatewayException(string message) : Exception(message);
