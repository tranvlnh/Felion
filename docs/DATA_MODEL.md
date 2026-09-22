# Felion data model

The PostgreSQL schema is owned by the TypeScript bot. IDs are UUIDs unless noted;
Discord snowflakes and StudentIds are stored as strings. Timestamps use UTC
`timestamp with time zone` values.

## Reference data

- `departments`: `id`, `name`, `slug`, timestamps. `slug` is unique.
- `generations`: `id`, `name`. `name` is unique; Generation is a lightweight Discord
  role tag with no code or lifecycle timestamps.

## People and probation

- `members`: StudentId, name, club email, DepartmentId, GenerationId, Position, and
  Status. Position is `Admin`, `Core`, or `Member`.
- `probation_candidates`: StudentId, name, DepartmentId, GenerationId, optional TeamId,
  and Status.
- `probation_teams`: name, optional Discord role ID, and active flag.
- `team_mentors`: TeamId + MemberId composite key. The application must ensure mentors
  are active Members; one Member may mentor multiple teams.
- `identity_registry`: StudentId primary key across Member and ProbationCandidate.
- `discord_identity_links`: unique Discord user and unique `(subject_type, subject_id)`.

The current schema uses strong all-row uniqueness for Member/Candidate StudentIds. The
intended product rule is “at most one active identity”; relaxing that to historical-row
reuse requires an explicit migration and decision.

## Discord roles and history

- `discord_role_mappings`: configurable role per Position, Probation, Department,
  Generation, or ProbationTeam key.
- `discord_role_assignments`: explicit per-subject managed role assignments.
- `audit_logs`: immutable-style privileged mutation history written by application
  workflows.
- `discord_sync_jobs`: retryable `KickUser` job records for failed probation users.

Role mapping storage exists today. Role assignment reconciliation and synchronization
are pending application work.

## Evaluation

- `evaluation_periods`: Open or Closed evaluation periods.
- `evaluation_criteria`: active score criteria per `Peer` or `Mentor` kind, with bounds
  and display order.
- `peer_evaluations`: candidate-to-candidate scores and one optional note.
- `mentor_evaluations`: mentor-to-candidate scores and one optional note.

Evaluation rows store score snapshots so later criterion changes do not rewrite history.
The current bot manages criteria but does not yet create or read evaluation submissions.

## Migration ownership

Schema changes are represented by ordered SQL files in `bot/drizzle/`. The current
sequence is:

| Migration | Purpose |
| --- | --- |
| `0000_cold_vengeance` | Initial target schema and enums |
| `0001_mysterious_harrier` | Global StudentId identity registry |
| `0002_configurable_evaluation_criteria` | Criterion table and score snapshots |
| `0003_simplify_generations` | Remove Generation code/lifecycle columns |
