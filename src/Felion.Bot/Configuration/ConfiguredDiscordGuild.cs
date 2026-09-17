namespace Felion.Bot.Configuration;

public sealed record ConfiguredDiscordGuild(ulong Id)
{
    public bool Matches(ulong? guildId) => guildId == Id;
}
