import { randomUUID } from 'node:crypto';
import type { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { writeAudit } from '#app/db/audit.js';
import { listEmailRecipients } from '#app/db/people.js';
import {
  addressModes, buildMime, htmlDocument, pickAddress, renderTemplate, resolveRecipients, templateKeys, templateVars,
  unknownPlaceholders,
} from '#app/domain/email.js';
import { AppError, getPublicErrorMessage } from '#app/errors.js';
import { contentTypePattern, createGmailSender, embedImages } from '#app/google/gmail.js';
import { assertCan } from '../permissions.js';
import type { WebContext } from '../server.js';
import { listQuery } from './people.js';

// Gmail caps a message at 25 MB after base64 (+33%).
const maxPayloadBytes = 15 * 1024 * 1024;
const maxConsecutiveFailures = 5;
const jobTtlMs = 60 * 60 * 1000;

const recipientsQuery = listQuery.omit({ page: true, pageSize: true }).extend({
  roleId: z.string().regex(/^\d{17,20}$/).optional(),
  address: z.enum(addressModes).default('clubThenPersonal'),
});

const sendBody = z.object({
  subject: z.string().trim().min(1).max(200),
  html: z.string().min(1),
  address: z.enum(addressModes),
  personIds: z.array(z.string().uuid()).max(500),
  attachments: z.array(z.object({
    filename: z.string().trim().min(1).max(200),
    contentType: z.string().max(100).regex(contentTypePattern),
    data: z.base64(),
  }).strict()).max(10).default([]),
  // Sends one copy to the sender's own club email, with their own values in the variables.
  test: z.boolean().default(false),
}).strict();

type Job = {
  id: string;
  actorId: string;
  total: number;
  sent: number;
  failed: { fullName: string; email: string; error: string }[];
  done: boolean;
  stopped: boolean;
};

// shortcut: jobs live in memory, so a restart mid-send loses progress and its audit entry; move to a table if history/resume is needed.
export function registerEmailRoutes(app: FastifyInstance, context: WebContext): void {
  const { db } = context;
  const sender = createGmailSender(context.config);
  const jobs = new Map<string, Job>();

  app.get('/email/recipients', async (request) => {
    assertCan(request.actor, 'SendEmail');
    const { address, ...filter } = recipientsQuery.parse(request.query);
    const people = await listEmailRecipients(db, filter);
    return {
      configured: sender !== null,
      sender: sender?.address ?? null,
      // People without an address of this kind can't be emailed, so they are not offered.
      items: people.flatMap((person) => {
        const email = pickAddress(person, address);
        return email ? [{ id: person.id, fullName: person.fullName, studentId: person.studentId, email }] : [];
      }),
    };
  });

  app.post('/email/send', { bodyLimit: 32 * 1024 * 1024 }, async (request, reply) => {
    assertCan(request.actor, 'SendEmail');
    if (!sender) throw new AppError('CONFLICT', 'Email sending is not configured on the server.');
    const body = sendBody.parse(request.body);

    const unknown = unknownPlaceholders(`${body.subject} ${body.html}`);
    if (unknown.length > 0) {
      throw new AppError('VALIDATION', `Unknown variables: ${unknown.map((key) => `{{${key}}}`).join(', ')}. Allowed: ${templateKeys.map((key) => `{{${key}}}`).join(', ')}.`);
    }
    if (!body.test && [...jobs.values()].some((job) => !job.done)) {
      throw new AppError('CONFLICT', 'Another bulk email is still sending. Wait for it to finish.');
    }

    const people = await listEmailRecipients(db, { ids: body.test ? [request.actor.id] : body.personIds });
    const recipients = resolveRecipients(people, body.test ? 'club' : body.address);
    if (recipients.length === 0) throw new AppError('VALIDATION', 'None of the selected people has an email address of that kind.');

    const { html, inline } = await embedImages(body.html);
    const attachments = body.attachments.map((file) => ({ ...file, data: Buffer.from(file.data, 'base64') }));
    const payloadBytes = [...inline, ...attachments].reduce((sum, part) => sum + part.data.length, 0);
    if (payloadBytes > maxPayloadBytes) throw new AppError('VALIDATION', 'Images and attachments must total at most 15 MB.');

    const mimeFor = ({ person, email }: (typeof recipients)[number]) => {
      const vars = templateVars(person, email);
      return buildMime({
        from: { email: sender.address, name: sender.name },
        to: email,
        subject: renderTemplate(body.subject, vars, false),
        html: htmlDocument(renderTemplate(html, vars, true)),
        inline,
        attachments,
      });
    };
    const audit = (sent: number, failed: number) => writeAudit(db, {
      actorId: request.actor.id,
      action: 'EmailSent',
      entityType: 'Email',
      metadata: { subject: body.subject, address: body.address, total: recipients.length, sent, failed, test: body.test },
    });

    if (body.test) {
      await sender.send(mimeFor(recipients[0]!), recipients[0]!.email);
      await audit(1, 0);
      return { sentTo: recipients[0]!.email };
    }

    const job: Job = { id: randomUUID(), actorId: request.actor.id, total: recipients.length, sent: 0, failed: [], done: false, stopped: false };
    jobs.set(job.id, job);
    void (async () => {
      let consecutiveFailures = 0;
      for (const recipient of recipients) {
        try {
          await sender.send(mimeFor(recipient), recipient.email);
          job.sent += 1;
          consecutiveFailures = 0;
        } catch (error) {
          job.failed.push({ fullName: recipient.person.fullName, email: recipient.email, error: getPublicErrorMessage(error, 'Send failed.') });
          // Usually a setup problem (wrong password, daily limit); don't burn through the whole list.
          if (++consecutiveFailures >= maxConsecutiveFailures) {
            job.stopped = true;
            break;
          }
        }
      }
      job.done = true;
      setTimeout(() => jobs.delete(job.id), jobTtlMs).unref();
      await audit(job.sent, job.failed.length).catch((error: unknown) => app.log.error({ err: error }, 'Email audit failed'));
    })();
    return reply.code(202).send({ jobId: job.id });
  });

  app.get('/email/jobs/:id', async (request) => {
    assertCan(request.actor, 'SendEmail');
    const { id } = z.object({ id: z.string().uuid() }).parse(request.params);
    const job = jobs.get(id);
    if (!job || (job.actorId !== request.actor.id && !request.actor.isAdmin)) throw new AppError('NOT_FOUND', 'Email job not found.');
    const { actorId: _actorId, ...status } = job;
    return status;
  });
}
