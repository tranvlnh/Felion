import { REST, Routes } from 'discord.js';
import type { Config } from '#app/config.js';
import { verificationCommands } from './verification.js';

// PUT replaces the whole set, so the retired slash commands disappear from the guild.
export async function registerGuildCommands(config: Config): Promise<void> {
  const rest = new REST({ version: '10' }).setToken(config.DISCORD_TOKEN);
  await rest.put(
    Routes.applicationGuildCommands(config.DISCORD_APPLICATION_ID, config.DISCORD_GUILD_ID),
    { body: verificationCommands.map((command) => command.toJSON()) },
  );
}
