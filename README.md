# Felion

One Node process for one Discord guild:

- **Web admin** (Fastify API in `backend/`, React SPA in `frontend/`): people, probation → member,
  Discord roles and their permissions, role sync, kick, departments, audit log. Sign-in is Google
  Workspace OAuth for anyone whose club email an admin has added.
- **Discord bot** (discord.js, in `backend/`): no slash commands; the StudentId
  button/modal (message published from the web Settings page) that links a Discord account to a person. It also restores roles when a linked person
  rejoins and forgets roles deleted in Discord.
- **PostgreSQL via Prisma 7** (`backend/prisma/schema.prisma`, migrations next to it).

In production the backend also serves `frontend/dist`, so the UI and the API share one origin.

## Layout

```
package.json        npm workspaces: backend, frontend
backend/
  prisma/           schema.prisma + migrations
  src/
    index.ts        starts Discord client + web server
    config.ts       env validation
    errors.ts       AppError + public error messages
    api/            Fastify: server, Google auth, permission rules, routes/
    db/             Prisma queries (people, roles, settings, departments, audit, linking)
    discord/        client, verification message, role sync, guild role CRUD, kick
    domain/         pure helpers (normalization, role sync plan)
    generated/      Prisma client (generated, ignored)
  scripts/          one-off: bootstrap admin
  tests/            Vitest
frontend/           React app (Vite)
Dockerfile, compose.yaml
```

## Model

- `Person` is both probation candidates (`kind=Candidate`) and members (`kind=Member`). Departments are
  just a unique name.
- Roles are the guild's real roles. `PersonRole` stores the roles each person *should* have; sync makes
  Discord match (only roles the bot can manage: not `@everyone`, not integration roles, below the bot).
- `RolePermission` gives a Discord role web permissions (`ViewPeople`, `ManagePeople`, `AssignRoles`,
  `KickMembers`). `Person.isAdmin` is the only web-only role and grants everything.
- Non-admins never act on admins, and cannot assign roles that carry web permissions or moderation
  Discord permissions. The last admin cannot be removed. See `backend/src/api/permissions.ts`.
- Generation is a number. The Probation page holds one round at a time: `ProbationTeam` (optional Discord
  role), candidates via `Person.teamId`, mentors (Members, any number of teams) via `ProbationTeamMentor`.
  Joining or leaving a team adds or removes its role. "Approve" only marks a candidate
  (`probationApproved`); nothing changes until "End round" (admin): approved candidates become Members
  (Probation and team roles swapped for the Member role chosen in Settings), every other candidate and every
  team is deleted and their Probation/team roles are stripped in Discord. The next round starts empty.

## Configuration

Copy `backend/.env.example` to `backend/.env` and fill it in. Discord: enable the **Server Members
Intent** for the bot, and put the bot's role above every role it should manage. Google: create an OAuth
client (Web application, consent screen *Internal*) with redirect URI
`<WEB_PUBLIC_URL>/auth/google/callback`.

## Development

From the repository root:

```powershell
npm ci                                         # both workspaces
docker compose --profile dev up -d postgres    # or any PostgreSQL
npm run db:deploy -w backend                   # apply migrations
npm run bootstrap-admin -w backend '--' you@club.edu --create --student-id SV001 --name "Your Name" --department "Tech" --generation 1
npm run dev                                    # bot + API on :3000
npm run dev:frontend                           # UI on :5173 (set WEB_PUBLIC_URL=http://localhost:5173)
npm test
npm run typecheck
npm run build                                  # backend/dist + frontend/dist
```

`npm run db:migrate -w backend` creates a new migration after a schema change; commit the SQL.

## Docker

```powershell
docker compose up -d --build
```

The image builds both workspaces, runs `prisma migrate deploy`, then starts on port 3000.
Configuration comes from `backend/.env`.
`GET /healthz` checks the database and backs the image's `HEALTHCHECK`.

## CSV

- **Import** ("Nhập từ CSV" on the Members and Probation pages, needs `ManagePeople`): the first row names
  the columns, using API field names: `studentId, fullName, department, generation` (required) and
  `phone, personalEmail, birthDate, facebookUrl, clubEmail` (optional; `clubEmail` is admin-only).
  `department` is a department name, `birthDate` is `YYYY-MM-DD`. The file is checked first (dry run); one bad
  row means nobody is added. Imported people get the default Probation/Member role from Settings, like
  people added by hand. Save from Excel as "CSV UTF-8". At most 500 rows.
- **Export** ("Xuất CSV" on the Members page, needs `ViewPeople`) uses the current filters and writes UTF-8 with
  a BOM. Cells starting with `= + - @` get a leading `'` so spreadsheets don't run them as formulas.
