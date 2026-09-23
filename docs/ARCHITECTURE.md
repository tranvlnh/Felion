# Felion architecture

Felion is a single TypeScript Discord bot process for one configured guild. Discord
handlers are transport adapters; domain modules hold invariants; Drizzle/PostgreSQL
provide persistence and transaction boundaries.

## Runtime boundaries

```text
Discord Gateway
      |
      v
bot/src/discord       client composition, interaction router, registration
      |
      v
bot/src/features/*/discord
                      feature command definitions, handlers, responses
      |
      v
bot/src/features/*/application
                      use cases and feature-specific persistence contracts
      |
      v
bot/src/shared        transport-independent application errors
bot/src/domain        normalization and business invariants
bot/src/db            Drizzle adapters, PostgreSQL access, transaction execution
      |
      v
PostgreSQL            schema, constraints, audit history, migrations
```

The bot registers commands for `DISCORD_GUILD_ID` and has no HTTP server, web
dashboard, browser authentication, or event module.

## Modules

- `src/config.ts`: environment validation for Discord, database, and runtime mode.
- `src/discord`: Discord client composition, first-match interaction routing, guild
  registration, and shared Discord integration helpers.
- `src/features/*/discord`: feature-owned slash-command definitions and thin
  interaction handlers for system, identity, members, probation, reference data,
  roles, and evaluations.
- `src/db/management-read-model.ts`: read-only management projections used by list and
  detail commands; these queries do not replace feature-owned mutation workflows.
- `src/features/evaluations/application`: evaluation use-case sequencing and its
  feature-specific persistence contract. This is the first application-layer vertical
  slice; remaining features continue to migrate incrementally.
- `src/domain`: Member normalization, evaluation criteria/score rules, probation
  evaluation eligibility rules, and managed-role desired-state reconciliation.
- `src/shared/errors`: application error codes and safe public-message extraction.
- `src/db/schema.ts`: Drizzle PostgreSQL schema and enums.
- `src/db/*.ts`: transactional bootstrap, linking, Member/reference/probation-team/
  probation-candidate administration, mentor assignment, authorization, and shared
  PostgreSQL access.
- `src/db/evaluations`: Drizzle implementation of the evaluation persistence contract;
  it owns schema queries and executes application-requested transactions.
- `drizzle/`: reviewed SQL migrations applied by the runtime/Docker entrypoint.
- `tests/`: focused Vitest domain tests.

Business rules must not be placed only in Discord handlers or inferred from Discord
role possession. Database constraints are required for identity uniqueness and
idempotent mutation boundaries.

The interaction router evaluates feature handlers in an explicit composition order and
stops after the first handler accepts an interaction. The central client does not
contain command-specific branching.

The Discord client is the explicit composition root for application services and their
Drizzle adapters. Application modules do not import Discord or Drizzle modules, and
persistence adapters depend on feature-specific contracts rather than generic
repositories.

Cross-module source imports use the private Node package alias `#app/*`. TypeScript maps
the alias to `src/*`, Vitest resolves it to source, and production Node resolves it to
`dist/*` through `package.json#imports`. Relative imports remain appropriate for files
inside the same feature boundary.

## Authorization boundary

Discord's Administrator permission gates the bootstrap and verification-publish command
definitions. Administrative Member, reference-data, role-mapping, criterion, and
evaluation-period handlers require a typed linked active Felion Admin actor. Shared
resolvers also define Core/Admin, Candidate, and Mentor actor boundaries for current and
next workflows. Evaluation submission handlers resolve a linked active candidate or
Member mentor and apply their domain eligibility rules. Felion authorization is
resolved from linked identity records; Discord roles alone are not the application
authorization source.

Raw evaluation views and exports require a linked active Core/Admin actor. The view is
ephemeral and keeps short-lived pagination state in the evaluation Discord adapter; Excel
exports contain a readable summary and full-period raw score sheet. These reads expose
stored evaluator names and raw snapshots without displaying UUIDs or creating audit
mutations.

Read-only management commands use the same linked-identity boundaries: Core/Admin may
inspect probation, teams, and evaluation operations; Admin may inspect Members and
reference/role administration; a probation candidate may inspect only their own
candidate profile. These reads do not create audit mutations.

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
