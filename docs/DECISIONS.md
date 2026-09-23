# Felion decisions

## ADR-001 — Discord-only single guild

The TypeScript bot is the only runtime and operates on one guild configured by
`DISCORD_GUILD_ID`. There is no web/API transport. Slash commands are registered with
the Discord guild route, never discovered from incoming request data.

## ADR-002 — Separate people aggregates

`Member` and `ProbationCandidate` are separate tables and identities. Promotion will
create a Member and transfer the Discord link; it will not mutate a candidate into a
Member row.

## ADR-003 — Lightweight Generation tags

Generation stores a stable ID, display name, and active flag. It exists to select the
configured Generation Discord role and has no code or lifecycle timestamps. Department
and Generation deactivation is soft: historical Member/Candidate references remain
valid, while inactive references cannot be assigned to new people.

## ADR-004 — Database-enforced identity uniqueness

StudentId and Discord-user uniqueness are enforced by PostgreSQL constraints and checked
again by transactional application workflows. The current schema uses a global
`identity_registry` for StudentId ownership across the two subject types.

## ADR-005 — Configurable evaluation criteria

Admins may add, rename, or deactivate score criteria per evaluation kind. Every future
submission keeps criterion ID/name/score snapshots plus one fixed optional text note.
Deactivation cannot remove the last active score criterion for a kind.

## ADR-006 — Manual probation decisions

Scores are advisory. Core/Admin manually decide PASS or FAIL. Both workflows must
synchronize Discord state and write audit history transactionally. The schema reserves
the required history/sync tables, but the workflows are still pending.

## ADR-007 — Thin Discord transport

Discord handlers parse interactions, resolve the actor, call application/database
workflows, and format responses. Domain invariants belong in `domain/`; transaction
boundaries and persistence belong in `db/`.

Command definitions and handlers are owned by feature modules. A small explicit router
composes them at the Discord client boundary and stops at the first matching handler.
Linked identities are resolved through typed actor boundaries (`Admin`, `Core/Admin`,
`Candidate`, and `Mentor`) rather than transport-level boolean checks. Expected
authorization failures use application errors with a safe public message, and Discord
handlers use a shared response helper while legacy workflow errors are migrated
incrementally.

## ADR-008 — Checked-in migration artifacts

Development may use Drizzle Kit to generate migrations, but production receives reviewed
SQL files committed under `bot/drizzle/`. The Docker entrypoint applies those files
before starting the bot.

## ADR-009 — Administrative candidate lifecycle

Only linked active Admins manage ProbationCandidates in the current command surface. A
candidate is created active without requiring a team and may later be assigned or moved
to an active team. General lifecycle management is limited to `Active`/`Inactive`;
`Passed` and `Failed` are reserved for the separate manual decision workflows. Team and
lifecycle changes immediately synchronize Discord roles for linked candidates and audit
both the database mutation and synchronization outcome. Inactive candidates retain
their identity link and stored explicit assignments but have no desired Felion-managed
Discord roles.

## ADR-010 — Feature-specific application boundaries

Complex features are migrated incrementally from transport-to-database calls to an
explicit application service. The application layer owns use-case sequencing,
transaction intent, domain-rule invocation, and audit intent. It depends on a
feature-specific persistence contract; a Drizzle adapter implements that contract and
owns SQL/schema details and the physical transaction.

The evaluation feature is the first migrated vertical slice. Dependency composition is
explicit in the Discord client composition root. Felion does not use a generic
repository abstraction, service locator, or dependency-injection framework.

## ADR-011 — Raw evaluation reporting

Linked active Core/Admin actors may read raw evaluations for both Open and Closed
periods. `/evaluation-report view` requires a period and candidate, supports an optional
Peer/Mentor filter, and returns an ephemeral paged response. `/evaluation-report export`
returns an ephemeral CSV attachment containing all Peer and Mentor evaluations in the
period.

Reports include evaluator ID/name, target ID/name, score snapshots, the fixed note, and
the UTC submission timestamp. Read access does not create an `AuditLog`; the current
audit requirement remains scoped to privileged mutations.

## ADR-012 — Node-native internal import alias

Cross-module imports use `#app/*`, mapped to `src/*` for TypeScript/Vitest and `dist/*`
for the compiled Node ESM runtime. Relative imports are retained only for local modules
within the same feature boundary. This uses Node's private package-import mechanism and
does not require an emitted-path rewriter such as `tsc-alias`.
