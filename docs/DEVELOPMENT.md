# Development guide

## Prerequisites

- Node.js 20 or later
- npm
- PostgreSQL for migration/runtime work
- A Discord application and one configured guild for live integration checks

## Install and configure

```powershell
cd bot
npm ci
```

Create `.env` in `bot/`:

```dotenv
DISCORD_TOKEN=replace-me
DISCORD_APPLICATION_ID=123456789012345678
DISCORD_GUILD_ID=123456789012345678
DATABASE_URL=postgres://postgres:postgres@localhost:5432/felion
NODE_ENV=development
```

Do not commit tokens, database passwords, or `.env` files.

## Build, test, and run

```powershell
npm run build
npm test -- --run
npm run migrate
npm run dev
```

`npm run migrate` runs the compiled migration entrypoint. For Drizzle Kit development,
use `npm run db:generate` after reviewing the schema and `npm run db:migrate` against a
local database. Commit generated SQL only after reviewing it.

## Change checklist

1. Read `docs/PROJECT_STATUS.md`, `docs/TODO.md`, `docs/DECISIONS.md`, and the relevant
   specification.
2. Keep Discord handlers thin; put invariants in `src/domain` and transactions in
   `src/db`.
3. Add a migration for every schema change.
4. Add authorization and audit coverage for privileged mutations.
5. Run `npm run build` and `npm test -- --run` from `bot/`.
6. Update the command reference, data model, TODO, and project status when behavior or
   schema changes.

## CI

`.github/workflows/ci.yml` installs from `bot/package-lock.json`, builds, and runs the
Vitest suite against a PostgreSQL service. CI does not require Discord credentials and
does not run production migrations.
