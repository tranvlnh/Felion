# Felion project status

Last verified: 2026-09-24

## Current milestone

TypeScript Discord-only bot — foundation and target-schema migration complete; core
administration, probation-candidate lifecycle, and managed-role synchronization flows
are partially wired, including evaluation-period administration and evaluation
submission/reporting flows.

## Verified in the repository

- TypeScript runtime using Node.js, discord.js, Drizzle ORM, and PostgreSQL.
- Guild-scoped slash-command registration through `DISCORD_GUILD_ID`.
- Drizzle migrations `0000`–`0004`, including the identity registry,
  configurable evaluation criteria, and soft-deactivation for reference data.
- Initial Admin bootstrap, StudentId linking, Member creation, Department/Generation
  creation/edit/deactivation, and Discord role-mapping administration.
- Probation team creation, rename, and soft-deactivation with Admin authorization and
  transactional audit logging.
- Active-Member assignment to active probation teams with Admin authorization,
  duplicate protection, and transactional audit logging.
- Probation candidate creation without a required team, active-team assignment or
  reassignment, and `Active`/`Inactive` lifecycle management with Admin authorization,
  transactional audit logging, and immediate role synchronization for linked users.
- Desired-state Discord role synchronization for linked Members/Candidates, including
  explicit assignment preservation, manual Admin reconciliation, mentor team-role
  synchronization, and outcome auditing.
- Criterion add/reactivate, rename, and deactivate workflows with audit rows.
- Evaluation period open/close workflows with Admin authorization and transactional
  audit logging.
- Peer and Mentor evaluation submission workflows with eligibility checks, score
  snapshots, duplicate protection, and audit logging.
- Core/Admin-only raw evaluation views with pagination and full-period Excel exports,
  including evaluator identity, score snapshots, notes, and UTC submission timestamps.
- Feature-owned Discord command definitions and interaction handlers composed through
  a small first-match interaction router.
- Authorization-aware Discord autocomplete for entity references; operators no longer
  need to know or enter UUIDs for teams, candidates, Members, references, criteria,
  periods, or role mappings.
- Read-only management commands for candidate/team/member/reference/evaluation
  summaries and details, plus `/role inspect` for current-vs-desired role diagnostics.
- Peer/Mentor evaluation commands now open an ephemeral period/candidate selector and
  Modal-based score entry, with a next-candidate workflow after submission.
- Mentor assignment now immediately synchronizes mapped ProbationTeam roles for the
  linked Member, and Mentor authorization/evaluation queries ignore inactive teams.
- Typed linked-identity actor resolvers for Admin, Core/Admin, Candidate, and Mentor
  authorization boundaries, plus centralized Discord error-response handling.
- Shared `Database` and `DbTransaction` types keep persistence signatures consistent
  without repeating inferred Drizzle transaction types.
- Cross-module imports use the Node-native `#app/*` package alias consistently in
  source and tests; local files within one feature retain relative imports.
- Evaluation commands now use a feature-owned application service and a dedicated
  persistence contract implemented by a Drizzle adapter; Discord UI state is owned by
  the evaluation feature.
- PostgreSQL uniqueness constraints for Member/Candidate StudentIds, Discord links,
  emails, reference names, and evaluation submissions.
- Dockerfile, Compose deployment, CI build/test workflow, and checked-in migrations.
- `npm run build` passes.
- Vitest: 19 files and 73 tests pass.

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

- Manual PASS/FAIL decisions, identity transfer, role synchronization, and kick retry
  processing.
- Production Discord integration checks and database-backed integration tests.
- Complete authorization/audit coverage for every future privileged mutation.

See [`TODO.md`](TODO.md) for the ordered backlog and [`COMMANDS.md`](COMMANDS.md) for
the currently registered command surface.
