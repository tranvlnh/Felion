import { describe, expect, it } from 'vitest';
import { buildWebServer } from '#app/api/server.js';
import { configSchema } from '#app/config.js';
import type { Db } from '#app/db/client.js';

const config = configSchema.parse({
  DISCORD_TOKEN: 'token',
  DISCORD_APPLICATION_ID: '1',
  DISCORD_GUILD_ID: '1',
  DATABASE_URL: 'postgresql://localhost/felion',
  NODE_ENV: 'test',
  WEB_PUBLIC_URL: 'http://localhost:3000',
  SESSION_SECRET: 'x'.repeat(32),
  GOOGLE_CLIENT_ID: 'id',
  GOOGLE_CLIENT_SECRET: 'secret',
  GOOGLE_WORKSPACE_DOMAIN: 'gdg.example.edu',
});

function server(queryRaw: () => Promise<unknown>) {
  const db = { $queryRaw: queryRaw } as unknown as Db;
  return buildWebServer({ config, db, getGuild: () => Promise.reject(new Error('no Discord in tests')) });
}

describe('web server', () => {
  it('reports health from the database and sets security headers', async () => {
    const response = await server(async () => [1]).inject({ method: 'GET', url: '/healthz' });
    expect(response.statusCode).toBe(200);
    expect(response.headers['x-frame-options']).toBe('DENY');
    expect(response.headers['x-content-type-options']).toBe('nosniff');
  });

  it('returns 503 when the database is down', async () => {
    const response = await server(async () => { throw new Error('down'); }).inject({ method: 'GET', url: '/healthz' });
    expect(response.statusCode).toBe(503);
  });

  it('still requires the CSRF header on the import route', async () => {
    const response = await server(async () => [1]).inject({ method: 'POST', url: '/api/people/import', payload: {} });
    expect(response.statusCode).toBe(403);
  });
});
