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
