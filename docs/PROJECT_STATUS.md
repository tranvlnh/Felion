# Felion project status

Last verified: 2026-09-22

## Current milestone

TypeScript Discord-only bot — foundation and target-schema migration complete; core
administration flows are partially wired.

## Verified in the repository

- TypeScript runtime using Node.js, discord.js, Drizzle ORM, and PostgreSQL.
- Guild-scoped slash-command registration through `DISCORD_GUILD_ID`.
- Drizzle migrations `0000`–`0003`, including the identity registry,
  configurable evaluation criteria, and name-only Generations.
- Initial Admin bootstrap, StudentId linking, Member creation, Department/Generation
  creation, and Discord role-mapping administration.
- Criterion add/reactivate, rename, and deactivate workflows with audit rows.
- PostgreSQL uniqueness constraints for Member/Candidate StudentIds, Discord links,
  emails, reference names, and evaluation submissions.
- Dockerfile, Compose deployment, CI build/test workflow, and checked-in migrations.
- `npm run build` passes.
- Vitest: 3 files and 10 tests pass.

## Target data baseline

The migration checkpoint records the following imported baseline from 2026-09-21:

- 99 Members
- 55 ProbationCandidates
- 6 Teams
- 12 mentor relationships
- 62 Discord identity links
- 19 role mappings
- 337 audit logs

These counts are a historical migration snapshot; they are not generated or verified by
the current bot test suite.

## Not implemented yet

- Department/Generation edit and lifecycle commands.
- Actual Discord role synchronization and explicit assignment reconciliation.
- Probation team, candidate, and mentor management commands/workflows.
- Evaluation periods and Peer/Mentor submission flows.
- Raw evaluation read/reporting with Core/Admin authorization.
- Manual PASS/FAIL decisions, identity transfer, role synchronization, and kick retry
  processing.
- Production Discord integration checks and database-backed integration tests.
- Complete authorization/audit coverage for every future privileged mutation.

See [`TODO.md`](TODO.md) for the ordered backlog and [`COMMANDS.md`](COMMANDS.md) for
the currently registered command surface.
