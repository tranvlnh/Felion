# Felion Discord Bot

Felion is a TypeScript Discord bot for managing one configured Discord guild.
PostgreSQL is the system of record and Discord is the only application transport.

The repository currently contains the bot foundation: database migrations, domain
validation, bootstrap/linking, reference-data administration, configurable evaluation
criteria, guild-scoped command registration, and focused domain tests. The remaining
probation workflows are tracked in [`docs/TODO.md`](docs/TODO.md).

## Product scope

- Members, Departments, and lightweight Generation tags with soft-deactivation
- StudentId-based Discord identity linking
- Configurable Discord role mappings
- Probation candidates, teams, mentors, evaluations, and manual PASS/FAIL decisions
  as the target product scope
- PostgreSQL audit history and retryable failed-probation kicks as the target scope

There is deliberately no web dashboard, HTTP API, browser authentication, Google OAuth,
Events, attendance, registration, or .NET runtime in this target.

## Local setup

Requirements: Node.js 20+, npm, and PostgreSQL.

```powershell
cd bot
npm ci
```

Create `bot/.env` (it is ignored by Git) with:

```dotenv
DISCORD_TOKEN=replace-me
DISCORD_APPLICATION_ID=123456789012345678
DISCORD_GUILD_ID=123456789012345678
DATABASE_URL=postgres://postgres:postgres@localhost:5432/felion
NODE_ENV=development
```

Build before applying migrations because the migration script runs from `dist`:

```powershell
npm run build
npm run migrate
npm run dev
```

The bot registers slash commands only for `DISCORD_GUILD_ID` and does not expose an
HTTP port.

## Docker

```powershell
cd bot
# Create and edit .env using the variables shown above.
docker compose up -d --build
docker compose logs -f felion-bot
```

The image applies checked-in Drizzle migrations before starting the bot. Generate and
review migrations during development; do not generate migrations in production.

## Verification

```powershell
cd bot
npm run build
npm test -- --run
```

See [`bot/README.md`](bot/README.md), [`docs/COMMANDS.md`](docs/COMMANDS.md), and
[`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) for operational details.
