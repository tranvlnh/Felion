import { describe, expect, it } from 'vitest';
import { configSchema } from '#app/config.js';

const base = {
  DISCORD_TOKEN: 'token',
  DISCORD_APPLICATION_ID: '1',
  DISCORD_GUILD_ID: '1',
  DATABASE_URL: 'postgresql://localhost/felion',
  WEB_PUBLIC_URL: 'http://localhost:3000',
  SESSION_SECRET: 'x'.repeat(32),
  GOOGLE_CLIENT_ID: 'id',
  GOOGLE_CLIENT_SECRET: 'secret',
  GOOGLE_WORKSPACE_DOMAIN: 'gdg.edu',
};
const parse = (env: Record<string, string>) => configSchema.safeParse({ ...base, ...env });

describe('email config', () => {
  it('is optional, and blank values count as unset', () => {
    expect(parse({}).success).toBe(true);
    expect(parse({ EMAIL_SENDER: '', EMAIL_APP_PASSWORD: '' }).data?.EMAIL_SENDER).toBeUndefined();
  });

  it('normalizes the sender and drops the spaces Google shows in the app password', () => {
    const config = parse({ EMAIL_SENDER: ' Club@GDG.edu ', EMAIL_APP_PASSWORD: 'abcd efgh ijkl mnop' }).data;
    expect(config?.EMAIL_SENDER).toBe('club@gdg.edu');
    expect(config?.EMAIL_APP_PASSWORD).toBe('abcdefghijklmnop');
  });

  it('rejects half a setup and senders outside the workspace', () => {
    expect(parse({ EMAIL_SENDER: 'club@gdg.edu' }).success).toBe(false);
    expect(parse({ EMAIL_APP_PASSWORD: 'abcdefghijklmnop' }).success).toBe(false);
    expect(parse({ EMAIL_SENDER: 'a@gmail.com', EMAIL_APP_PASSWORD: 'abcdefghijklmnop' }).success).toBe(false);
  });
});
