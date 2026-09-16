namespace Felion.Bot;

public sealed record ConfiguredDiscordGuild(ulong Id)
{
    public bool Matches(ulong? guildId) => guildId == Id;
}
