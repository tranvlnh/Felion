# Felion architecture

Felion is a single TypeScript Discord bot process for one configured guild. Discord
handlers are transport adapters; domain modules hold invariants; Drizzle/PostgreSQL
provide persistence and transaction boundaries.

## Runtime boundaries

```text
Discord Gateway
      |
      v
bot/src/discord       slash commands, buttons, modals, responses
      |
      v
bot/src/domain        normalization and business invariants
bot/src/db            application workflows and PostgreSQL access
      |
      v
PostgreSQL            schema, constraints, audit history, migrations
```

The bot registers commands for `DISCORD_GUILD_ID` and has no HTTP server, web
dashboard, browser authentication, or event module.

## Modules

- `src/config.ts`: environment validation for Discord, database, and runtime mode.
- `src/discord`: command definitions, guild registration, interaction handling, and
  verification UI.
- `src/domain`: Member normalization, evaluation criteria/score rules, probation
  evaluation eligibility rules, and managed-role desired-state reconciliation.
- `src/db/schema.ts`: Drizzle PostgreSQL schema and enums.
- `src/db/*.ts`: transactional bootstrap, linking, Member/reference/probation-team/
  probation-candidate administration, mentor assignment, authorization, and criterion
  administration.
- `drizzle/`: reviewed SQL migrations applied by the runtime/Docker entrypoint.
- `tests/`: focused Vitest domain tests.

Business rules must not be placed only in Discord handlers or inferred from Discord
role possession. Database constraints are required for identity uniqueness and
idempotent mutation boundaries.

## Authorization boundary

Discord's Administrator permission gates the bootstrap and verification-publish command
definitions. The implemented Member, reference-data, role-mapping, and criterion
handlers additionally require a linked active Felion Admin. Felion authorization is
resolved from the linked Discord identity and active Member record; Discord roles alone
are not the application authorization source.

The `/role sync` workflow is restricted to linked active Admins. Automatic role
synchronization after StudentId linking uses the configured guild, not a guild inferred
from the interaction. Probation-candidate team and lifecycle changes are also restricted
to linked active Admins and immediately synchronize a linked candidate through that same
configured-guild boundary. Future Core/Admin workflows must reuse the same
linked-identity model and enforce their domain-specific authorization in application
modules.

## Deployment

The bot runs as one long-lived Docker container. PostgreSQL may be hosted by Supabase or
another PostgreSQL provider. The image compiles the bot, copies checked-in migrations,
applies them at startup, and then starts the Discord Gateway client.
