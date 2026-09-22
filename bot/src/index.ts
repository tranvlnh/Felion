import { loadConfig } from './config.js';
import { createDatabase } from './db/client.js';
import { createDiscordClient } from './discord/client.js';
import { registerGuildCommands } from './discord/register.js';

async function main(): Promise<void> {
  const config = loadConfig();
  const database = createDatabase(config);

  await registerGuildCommands(config);
  const client = createDiscordClient(config, database);
  await client.login(config.DISCORD_TOKEN);
}

main().catch((error: unknown) => {
  console.error(error);
  process.exitCode = 1;
});
