# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Felion is one Node process for one Discord guild (a GDG club): a Fastify web admin API + discord.js bot +
Prisma/PostgreSQL in `backend/`, and a Vite React SPA in `frontend/`. In production the backend serves
`frontend/dist`, so UI and API share one origin. `README.md` covers setup, Discord/Google configuration
and Docker in detail.

## Commands

Run from the repo root (npm workspaces: `backend`, `frontend`):

```powershell
npm ci
docker compose --profile dev up -d postgres   # local PostgreSQL
npm run db:deploy -w backend                  # apply migrations
npm run dev                                   # bot + API on :3000 (tsx watch)
npm run dev:frontend                          # UI on :5173, proxies /api and /auth to :3000
npm test                                      # backend Vitest suite
npm run typecheck                             # backend src + scripts + tests (tsconfig.check.json)
npm run build                                 # prisma generate + tsc, then frontend tsc + vite build
```

- Single test file: `npm test -w backend '--' tests/permissions.test.ts`; by name: add `-t "<name>"`. In PowerShell quote the separator (`'--'`), or npm swallows the script's flags.
- Schema change: edit `backend/prisma/schema.prisma`, run `npm run db:migrate -w backend`, commit the SQL.
- The Prisma client is generated into `backend/src/generated/` (gitignored). Run `npm run db:generate -w backend`
  after a fresh clone or schema change, or imports from `#app/generated/prisma/*` fail.
- When running the frontend dev server, set `WEB_PUBLIC_URL=http://localhost:5173` in `backend/.env` so the
  Google OAuth callback goes through the Vite proxy.
- CI (`.github/workflows/ci.yml`) runs build → typecheck → test with no database, Discord or Google, so tests
  must stay pure logic.

## Architecture

**Entry** `backend/src/index.ts`: load env (`config.ts`, zod) → Prisma client → register guild slash commands
→ log in the Discord client → start Fastify. The web server gets the guild via `client.guilds.fetch`, so API
routes call Discord directly.

**Backend layers** (`backend/src/`):
- `api/` — Fastify. `server.ts` wires the cookie session, CSRF guard, error mapping and SPA fallback;
  `auth.ts` is Google OAuth + signed session cookie; `permissions.ts` holds all authorization rules;
  `routes/` register handlers under `/api` behind `requireSession`, which sets `request.actor`.
- `db/` — Prisma queries per area; `writeAudit` records actions in `AuditLog`.
- `discord/` — client, verification (no slash commands; the web Settings page publishes the StudentId button/modal message that links a Discord
  account to a Person), role sync, guild role CRUD, kick.
- `domain/` — pure helpers (e.g. `planRoleSync`), the main target for unit tests.
- Imports use the `#app/*` alias: tsconfig `paths` → `src/*` for tsc/tsx/vitest, and package.json `imports`
  → `dist/*` at runtime. Use `.js` extensions (NodeNext ESM).

**Data model** (see README "Model"):
- `Person` is both probation candidates and members (`kind`). Generation is a number (no Generation table).
- Probation (`db/probation.ts`, `routes/probation.ts`, `/probation` page) holds one round at a time: `ProbationTeam`
  (optional unique Discord role), candidates via `Person.teamId`, mentors (Members) via `ProbationTeamMentor`.
  Team membership writes the team role into `PersonRole`. Approval (`probationApproved`) is only a mark — there is
  no immediate promotion. Ending the round (admin, `splitRound` in `domain/probation.ts`) promotes approved candidates
  (Probation + team roles → Member role) and deletes all teams and other candidates; old probation data is never reused.
- `Department` is just a unique name (no slug).
- Roles are the guild's real Discord roles — no mapping table. `PersonRole` stores the roles a person *should*
  have, and sync pushes DB → Discord (`discord/role-sync.ts`: `planRoleSync` diff, then add/remove only roles the
  bot can manage). Roles deleted in Discord are removed from the DB.
- `RolePermission` gives a Discord role web permissions (`ViewPeople`, `ManagePeople`, `AssignRoles`,
  `KickMembers`). `Person.isAdmin` is the only web-only role and grants everything.

**Authorization invariants** (enforce in routes via `permissions.ts`, don't reimplement inline):
non-admins never act on admins (`assertCanActOn`); delegated `AssignRoles` cannot grant roles carrying web
permissions or dangerous Discord permissions (`assertCanAssignRole`); the last admin cannot lose admin.

**API conventions**: every mutating `/api/*` and `/auth/logout` request must send header `x-felion-csrf: 1`
(`frontend/src/api.ts` does this). Throw `AppError(code, message)` from `errors.ts` — the error handler maps codes
to HTTP status and returns `{ error }`; zod errors become 400.

**Frontend** (`frontend/src/`): React 19 + react-router + TanStack Query + Tailwind 4. `api.ts` hand-mirrors
the backend response types (no shared package), so update both sides when an API shape changes. UI text is
Vietnamese.

**Scripts** (`backend/scripts/`): `bootstrap-admin.ts` creates the first admin.

## Design decisions

The project was rewritten from a Discord-slash-command bot into this web panel. Don't reintroduce role-mapping
tables, a Generation table, password auth or evaluations without asking — they were deliberately removed.
`ProbationTeam` is the one deliberate exception (user-approved 2026-11-01).
