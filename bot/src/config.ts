import 'dotenv/config';
import { z } from 'zod';

const configSchema = z.object({
  DISCORD_TOKEN: z.string().min(1),
  DISCORD_APPLICATION_ID: z.string().regex(/^\d+$/),
  DISCORD_GUILD_ID: z.string().regex(/^\d+$/),
  DATABASE_URL: z.string().url(),
  NODE_ENV: z.enum(['development', 'test', 'production']).default('development'),
});

export type Config = z.infer<typeof configSchema>;

export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
  return configSchema.parse(env);
}
