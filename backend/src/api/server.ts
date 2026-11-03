import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import fastifyCookie from '@fastify/cookie';
import fastifyStatic from '@fastify/static';
import type { Client, Guild } from 'discord.js';
import Fastify, { type FastifyInstance } from 'fastify';
import { ZodError } from 'zod';
import type { Config } from '#app/config.js';
import type { Db } from '#app/db/client.js';
import { AppError, getPublicErrorMessage, type AppErrorCode } from '#app/errors.js';
import { registerAuth, requireSession } from './auth.js';
import type { Actor } from './permissions.js';
import { registerEmailRoutes } from './routes/email.js';
import { registerMiscRoutes } from './routes/misc.js';
import { registerPeopleRoutes } from './routes/people.js';
import { registerPeopleCsvRoutes } from './routes/people-csv.js';
import { registerProbationRoutes } from './routes/probation.js';
import { registerRoleRoutes } from './routes/roles.js';

declare module 'fastify' {
  interface FastifyRequest {
    actor: Actor;
  }
}

export type WebContext = {
  config: Config;
  db: Db;
  getGuild: () => Promise<Guild>;
};

const statusByCode: Record<AppErrorCode, number> = {
  FORBIDDEN: 403,
  NOT_FOUND: 404,
  CONFLICT: 409,
  VALIDATION: 400,
  EXTERNAL_SERVICE: 502,
};

const csrfHeader = 'x-felion-csrf';

export function buildWebServer(context: WebContext): FastifyInstance {
  const app = Fastify({ logger: { level: context.config.NODE_ENV === 'production' ? 'info' : 'debug' } });
  app.register(fastifyCookie, { secret: context.config.SESSION_SECRET });
  app.decorateRequest('actor', null as unknown as Actor);

  // A custom header cannot be sent cross-site without CORS, which we never enable.
  app.addHook('onRequest', async (request, reply) => {
    const mutating = !['GET', 'HEAD', 'OPTIONS'].includes(request.method);
    const guarded = request.url.startsWith('/api/') || request.url.startsWith('/auth/logout');
    if (mutating && guarded && request.headers[csrfHeader] !== '1') {
      await reply.code(403).send({ error: 'Missing CSRF header.' });
    }
  });

  app.addHook('onSend', async (_request, reply) => {
    reply.header('x-content-type-options', 'nosniff');
    reply.header('x-frame-options', 'DENY');
    reply.header('referrer-policy', 'same-origin');
  });

  app.setErrorHandler((error, request, reply) => {
    if (error instanceof AppError) {
      return reply.code(statusByCode[error.code]).send({ error: error.publicMessage });
    }
    if (error instanceof ZodError) {
      const issue = error.issues[0];
      return reply.code(400).send({ error: `${issue?.path.join('.') || 'body'}: ${issue?.message ?? 'invalid'}` });
    }
    const statusCode = (error as { statusCode?: number }).statusCode;
    if (statusCode && statusCode < 500) {
      return reply.code(statusCode).send({ error: (error as Error).message });
    }
    request.log.error({ err: error }, 'Request failed');
    return reply.code(500).send({ error: getPublicErrorMessage(error, 'Something went wrong.') });
  });

  registerAuth(app, context);

  // For the Docker HEALTHCHECK; no session needed.
  app.get('/healthz', async (request, reply) => {
    try {
      await context.db.$queryRaw`SELECT 1`;
      return { ok: true };
    } catch (error) {
      request.log.error({ err: error }, 'Health check failed');
      return reply.code(503).send({ ok: false });
    }
  });

  app.register(async (api) => {
    api.addHook('preHandler', (request, reply) => requireSession(context, request, reply));
    registerPeopleRoutes(api, context);
    registerPeopleCsvRoutes(api, context);
    registerProbationRoutes(api, context);
    registerRoleRoutes(api, context);
    registerMiscRoutes(api, context);
    registerEmailRoutes(api, context);
  }, { prefix: '/api' });

  // backend/{src,dist}/api -> frontend/dist; both layouts sit at the same depth.
  const webRoot = fileURLToPath(new URL('../../../frontend/dist', import.meta.url));
  if (existsSync(webRoot)) {
    app.register(fastifyStatic, { root: webRoot });
    app.setNotFoundHandler((request, reply) => {
      const path = request.url.split('?')[0] ?? '';
      // Client-side routes get the SPA shell; a missing asset stays a 404 instead of returning HTML.
      if (request.method === 'GET' && !path.startsWith('/api/') && !path.startsWith('/auth/') && !/\.[a-z0-9]+$/i.test(path)) {
        return reply.sendFile('index.html');
      }
      return reply.code(404).send({ error: 'Not found.' });
    });
  }

  return app;
}

export async function startWebServer(config: Config, db: Db, client: Client): Promise<FastifyInstance> {
  const app = buildWebServer({ config, db, getGuild: () => client.guilds.fetch(config.DISCORD_GUILD_ID) });
  await app.listen({ port: config.WEB_PORT, host: '0.0.0.0' });
  return app;
}
