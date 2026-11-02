import { REST, Routes } from 'discord.js';
import type { Config } from '#app/config.js';

// The bot has no slash commands (the verification message is published from the web).
// PUT replaces the whole set, so retired commands such as /verification disappear from the guild.
export async function registerGuildCommands(config: Config): Promise<void> {
  const rest = new REST({ version: '10' }).setToken(config.DISCORD_TOKEN);
  await rest.put(Routes.applicationGuildCommands(config.DISCORD_APPLICATION_ID, config.DISCORD_GUILD_ID), { body: [] });
}
