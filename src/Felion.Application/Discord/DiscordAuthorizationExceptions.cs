namespace Felion.Application.Discord;

public sealed class DiscordAuthorizationException()
    : Exception("Only an active Admin member linked to Discord may use this command.");
