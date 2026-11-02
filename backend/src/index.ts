import { loadConfig } from './config.js';
import { createPrismaClient } from './db/client.js';
import { createDiscordClient } from './discord/client.js';
import { registerGuildCommands } from './discord/register.js';
import { startWebServer } from './api/server.js';

async function main(): Promise<void> {
  const config = loadConfig();
  const db = createPrismaClient(config.DATABASE_URL);

  await registerGuildCommands(config);
  const client = createDiscordClient(config, db);
  await client.login(config.DISCORD_TOKEN);
  const web = await startWebServer(config, db, client);

  const shutdown = async () => {
    await web.close();
    await client.destroy();
    await db.$disconnect();
  };
  process.once('SIGTERM', () => void shutdown());
  process.once('SIGINT', () => void shutdown());
}

main().catch((error: unknown) => {
  console.error(error);
  // exit() rather than exitCode: a logged-in Discord client keeps the process alive.
  process.exit(1);
});
