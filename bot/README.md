# Felion Discord Bot

The `bot` directory contains the TypeScript/discord.js runtime for one configured
guild. Drizzle ORM owns PostgreSQL access and checked-in SQL migrations are the database
deployment artifact.

## Implemented foundation

- Guild-scoped slash-command registration and Discord Gateway runtime
- Initial Admin bootstrap when the database has no Members
- StudentId-based identity linking with audit logging
- Member creation with Department/Generation references
- Department and name-only Generation creation
- Discord role mapping storage and administration
- Configurable Peer/Mentor score criteria with rename/deactivate/reactivate support
- Domain tests for member normalization, probation evaluation rules, and score criteria

The following are schema/target scope but are not wired into commands yet: probation
team/candidate/mentor management, evaluation submission, role synchronization, manual
PASS/FAIL decisions, and failed-probation kick retries.

There is deliberately no HTTP API, web dashboard, browser authentication, Google OAuth,
Events, EventPosition, EventRegistration, or EventAttendance model in this application.

## Configuration

Create `bot/.env` locally; it is ignored by Git:

```dotenv
DISCORD_TOKEN=replace-me
DISCORD_APPLICATION_ID=123456789012345678
DISCORD_GUILD_ID=123456789012345678
DATABASE_URL=postgres://postgres:postgres@localhost:5432/felion
NODE_ENV=development
```

`DISCORD_GUILD_ID` is mandatory. Commands are registered only for that guild. The
configuration loader validates all four required service values and `NODE_ENV`.

## Development commands

```powershell
npm ci
npm run build
npm run migrate
npm run dev
npm test -- --run
```

`npm run db:generate` creates a reviewed Drizzle migration during development and
`npm run db:migrate` applies migrations directly through Drizzle Kit. The runtime
`npm run migrate` script applies the checked-in migrations from `dist/drizzle` and is
what the Docker image uses after compiling.

## Docker deployment

The container is a long-running Discord Gateway process and does not expose an HTTP
port. PostgreSQL can be Supabase or another managed PostgreSQL provider.

```powershell
docker compose up -d --build
docker compose logs -f felion-bot
```

The container applies checked-in migrations before starting the bot. Do not run
`db:generate` in production; generate and review migrations during development, then
commit the SQL files before rebuilding the image.
