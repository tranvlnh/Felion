import { randomBytes } from 'node:crypto';
import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { OAuth2Client } from 'google-auth-library';
import { writeAudit } from '#app/db/audit.js';
import { findLoginableByEmail } from '#app/db/people.js';
import { loadActor } from '#app/db/roles.js';
import type { WebContext } from './server.js';

export const sessionCookie = 'felion_session';
const stateCookie = 'felion_oauth_state';
const sessionMaxAgeSeconds = 7 * 24 * 60 * 60;

export type GoogleClaims = { email?: string; email_verified?: boolean; hd?: string };

export function checkGoogleClaims(claims: GoogleClaims | undefined, workspaceDomain: string): string | null {
  const email = claims?.email?.trim().toLowerCase();
  if (!email || claims?.email_verified !== true) return null;
  if (claims.hd?.toLowerCase() !== workspaceDomain || !email.endsWith(`@${workspaceDomain}`)) return null;
  return email;
}

export function encodeSession(personId: string, now: number): string {
  return `${personId}.${now + sessionMaxAgeSeconds * 1000}`;
}

export function parseSession(value: string, now: number): string | null {
  const match = /^([0-9a-f-]{36})\.(\d+)$/.exec(value);
  if (!match || Number(match[2]) <= now) return null;
  return match[1] ?? null;
}

function cookieOptions(context: WebContext, path: string, maxAge: number) {
  return {
    path,
    maxAge,
    httpOnly: true,
    sameSite: 'lax' as const,
    secure: context.config.WEB_PUBLIC_URL.startsWith('https://'),
    signed: true,
  };
}

export function registerAuth(app: FastifyInstance, context: WebContext): void {
  const { config, db } = context;
  const oauth = new OAuth2Client({
    clientId: config.GOOGLE_CLIENT_ID,
    clientSecret: config.GOOGLE_CLIENT_SECRET,
    redirectUri: `${config.WEB_PUBLIC_URL}/auth/google/callback`,
  });

  app.get('/auth/google', async (_request, reply) => {
    const state = randomBytes(24).toString('base64url');
    reply.setCookie(stateCookie, state, cookieOptions(context, '/auth', 600));
    return reply.redirect(oauth.generateAuthUrl({
      scope: ['openid', 'email', 'profile'],
      state,
      hd: config.GOOGLE_WORKSPACE_DOMAIN,
      prompt: 'select_account',
    }));
  });

  app.get<{ Querystring: { code?: string; state?: string } }>('/auth/google/callback', async (request, reply) => {
    const fail = (reason: string) => reply.redirect(`/login?error=${reason}`);
    const raw = request.cookies[stateCookie];
    reply.clearCookie(stateCookie, { path: '/auth' });

    const stored = raw ? request.unsignCookie(raw) : null;
    if (!stored?.valid || !request.query.state || stored.value !== request.query.state || !request.query.code) {
      return fail('state');
    }

    let email: string | null;
    try {
      const { tokens } = await oauth.getToken(request.query.code);
      if (!tokens.id_token) return fail('google');
      const ticket = await oauth.verifyIdToken({ idToken: tokens.id_token, audience: config.GOOGLE_CLIENT_ID });
      email = checkGoogleClaims(ticket.getPayload(), config.GOOGLE_WORKSPACE_DOMAIN);
    } catch (error) {
      request.log.warn({ err: error }, 'Google sign-in failed');
      return fail('google');
    }
    if (!email) return fail('domain');

    const person = await findLoginableByEmail(db, email);
    if (!person) return fail('not-member');

    reply.setCookie(sessionCookie, encodeSession(person.id, Date.now()), cookieOptions(context, '/', sessionMaxAgeSeconds));
    await writeAudit(db, { actorId: person.id, action: 'WebSignIn', entityType: 'Person', entityId: person.id });
    return reply.redirect('/');
  });

  app.post('/auth/logout', async (_request, reply) => {
    reply.clearCookie(sessionCookie, { path: '/' });
    return reply.code(204).send();
  });
}

export async function requireSession(context: WebContext, request: FastifyRequest, reply: FastifyReply): Promise<void> {
  const raw = request.cookies[sessionCookie];
  const unsigned = raw ? request.unsignCookie(raw) : null;
  const personId = unsigned?.valid && unsigned.value ? parseSession(unsigned.value, Date.now()) : null;
  const actor = personId ? await loadActor(context.db, personId) : null;
  if (!actor) {
    reply.clearCookie(sessionCookie, { path: '/' });
    await reply.code(401).send({ error: 'Please sign in.' });
    return;
  }
  request.actor = actor;
}
