# Felion product specification

Felion manages one configured Discord guild. Discord is the only management interface;
PostgreSQL is the system of record.

This document describes the target product behavior. The implementation checkpoint is
tracked separately in [`PROJECT_STATUS.md`](PROJECT_STATUS.md); schema presence does not
mean that a Discord workflow is already available.

## Access and identity

- Admin has full management access.
- Core manages members, probation, and evaluations within the permitted workflows.
- Member can link identity and use permitted self-service interactions.
- Probation candidates do not have management access.

Authorization is resolved from the linked Discord identity and active Member record.
Discord role possession alone is not authorization. Bootstrap and initial verification
publication may additionally be gated by Discord Administrator permission.

## Members and identity

Members are official club people with StudentId, full name, club Workspace email,
Department, Generation, Position, and Status. Department and Generation are separate;
Generation is selected by name and supplies a Discord role tag.

`Member` and `ProbationCandidate` are separate aggregates. At most one active Member or
ProbationCandidate may own a StudentId, and at most one active subject may own a Discord
user. Discord linking requires only StudentId.

Positions are `Admin`, `Core`, and `Member`. Probation is a separate aggregate/status,
not a position.

## Discord roles

Role mappings are configurable for Position, Probation, Department, Generation, and
ProbationTeam. Explicit Admin-managed role assignments must be preserved across identity
linking and promotion. Role mutations and synchronization outcomes are audited.

## Probation

`ProbationCandidate` is separate from `Member`. A candidate may belong to at most one
active team. A team may have multiple mentors; every mentor must be an active Member and
may mentor multiple teams.

## Evaluation

Evaluation periods are Open or Closed. Admin manages periods and score criteria.

- Peer evaluation targets another candidate in the same team, never the evaluator.
- Mentor evaluation targets candidates in teams mentored by the evaluator.
- A submission has exactly one score for every active criterion and one optional fixed
  text note.
- Criterion ID, name, and score snapshots preserve historical meaning after criteria
  are renamed or deactivated.
- Candidates cannot read raw results; Core/Admin can read them.
- Scores never automatically determine PASS or FAIL.

## Decisions, synchronization, and audit

Core/Admin manually decide PASS or FAIL.

- PASS creates a Member, transfers the Discord identity, synchronizes managed roles, and
  records an audit transaction.
- FAIL records an audit, kicks the Discord user, and retains historical evaluation data
  according to the configured retention policy. A failed kick is retryable.
- Every privileged mutation produces an `AuditLog`.

## Current implementation boundary

The current bot has working foundation/admin flows for bootstrap, StudentId linking,
regular Member creation, Department/Generation creation, role mapping, and evaluation
criterion administration. The following target behavior remains pending: probation
management, evaluation periods/submissions/reads, role synchronization, PASS/FAIL
decisions, kick retries, and complete Core authorization.
