import type { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { listAudit } from '#app/db/audit.js';
import { createDepartment, listDepartments, updateDepartment } from '#app/db/departments.js';
import { getPerson } from '#app/db/people.js';
import { fetchGuildMember } from '#app/discord/role-sync.js';
import { AppError } from '#app/errors.js';
import { assertAdmin, assertCan } from '../permissions.js';
import type { WebContext } from '../server.js';

const departmentBody = z.object({
  name: z.string(),
  active: z.boolean(),
}).partial().strict();

export function registerMiscRoutes(app: FastifyInstance, context: WebContext): void {
  const { db } = context;

  app.get('/me', async (request) => ({
    person: await getPerson(db, request.actor.id),
    isAdmin: request.actor.isAdmin,
    permissions: [...request.actor.permissions].sort(),
  }));

  // Guild member avatar (server avatar if set); the browser caches the redirect for an hour.
  app.get('/avatars/:discordUserId', async (request, reply) => {
    const { discordUserId } = z.object({ discordUserId: z.string().regex(/^\d{17,20}$/) }).parse(request.params);
    const member = await fetchGuildMember(await context.getGuild(), discordUserId);
    if (!member) throw new AppError('NOT_FOUND', 'Not in server');
    return reply.header('cache-control', 'private, max-age=3600').redirect(member.displayAvatarURL({ size: 128 }));
  });

  app.get('/departments', async () => listDepartments(db));

  app.post('/departments', async (request, reply) => {
    assertCan(request.actor, 'ManagePeople');
    const body = departmentBody.required({ name: true }).parse(request.body);
    return reply.code(201).send(await createDepartment(db, body, request.actor.id));
  });

  app.patch('/departments/:id', async (request) => {
    assertCan(request.actor, 'ManagePeople');
    const { id } = z.object({ id: z.string().uuid() }).parse(request.params);
    return updateDepartment(db, id, departmentBody.parse(request.body), request.actor.id);
  });

  app.get('/audit', async (request) => {
    assertAdmin(request.actor);
    const query = z.object({
      page: z.coerce.number().int().min(1).default(1),
      pageSize: z.coerce.number().int().min(1).max(100).default(50),
    }).parse(request.query);
    return listAudit(db, query.page, query.pageSize);
  });
}
