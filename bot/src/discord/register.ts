import { REST, Routes } from 'discord.js';
import type { Config } from '../config.js';
import { commandDefinitions } from './commands.js';

export async function registerGuildCommands(config: Config): Promise<void> {
  const rest = new REST({ version: '10' }).setToken(config.DISCORD_TOKEN);
  await rest.put(
    Routes.applicationGuildCommands(config.DISCORD_APPLICATION_ID, config.DISCORD_GUILD_ID),
    { body: commandDefinitions },
  );
}
