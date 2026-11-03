import 'dotenv/config';
import { z } from 'zod';

export const configSchema = z.object({
  DISCORD_TOKEN: z.string().min(1),
  DISCORD_APPLICATION_ID: z.string().regex(/^\d+$/),
  DISCORD_GUILD_ID: z.string().regex(/^\d+$/),
  DATABASE_URL: z.string().url(),
  NODE_ENV: z.enum(['development', 'test', 'production']).default('development'),
  WEB_PORT: z.coerce.number().int().positive().default(3000),
  WEB_PUBLIC_URL: z.string().url().transform((url) => url.replace(/\/+$/, '')),
  SESSION_SECRET: z.string().min(32),
  GOOGLE_CLIENT_ID: z.string().min(1),
  GOOGLE_CLIENT_SECRET: z.string().min(1),
  GOOGLE_WORKSPACE_DOMAIN: z.string().min(1).transform((domain) => domain.toLowerCase()),
  // Bulk email (optional): Gmail SMTP as EMAIL_SENDER with a Google App Password.
  EMAIL_SENDER: z.string().trim().toLowerCase().optional().transform((email) => email || undefined).pipe(z.email().optional()),
  // Google shows it in groups of four; the spaces are not part of it.
  EMAIL_APP_PASSWORD: z.string().optional().transform((password) => password?.replace(/\s+/g, '') || undefined),
  EMAIL_SENDER_NAME: z.string().trim().optional(),
}).superRefine((config, ctx) => {
  if (!config.EMAIL_APP_PASSWORD !== !config.EMAIL_SENDER) {
    ctx.addIssue({ code: 'custom', path: ['EMAIL_SENDER'], message: 'set both EMAIL_SENDER and EMAIL_APP_PASSWORD, or neither' });
  }
  if (config.EMAIL_SENDER && !config.EMAIL_SENDER.endsWith(`@${config.GOOGLE_WORKSPACE_DOMAIN}`)) {
    ctx.addIssue({ code: 'custom', path: ['EMAIL_SENDER'], message: 'must be an address in GOOGLE_WORKSPACE_DOMAIN' });
  }
});

export type Config = z.infer<typeof configSchema>;

export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
  return configSchema.parse(env);
}
