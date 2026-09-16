# Implementation Plan

## Pre-agent — Manual skeleton
The user creates `Felion.slnx`, the six approved source projects, the three approved test projects, and project references defined by ADR-013. The agent does not own this structural bootstrap.

## Milestone 0 — Foundation Setup
Validate the existing skeleton and dependency direction without reorganizing it. Configure central package management, nullable/analyzers, PostgreSQL EF provider, OpenAPI, health checks, DI/module registration conventions, configuration, logging/ProblemDetails, test infrastructure, CI, and development/secrets documentation. Do not implement business features in this milestone.

## Milestone 1 — Persistence + core domain
Implement Department, Generation, Member, ProbationCandidate, DiscordIdentityLink, AuditLog and migrations. Add unique constraints and normalization. Seed only configuration/reference data, never real secrets.

## Milestone 2 — Discord linking and role sync
Host NetCord in the same application. Implement verification button/modal, link use case, role mapping, role creation, role synchronization and privileged Discord commands. Add retryable DiscordSyncJob for side effects.

## Milestone 3 — Google Workspace authentication
Configure Google auth, resolve returned email to active Member, issue application principal with MemberId/Position claims, implement policies and `/me`. Reject unknown/inactive members even with valid Workspace-domain account.

## Milestone 4 — Member admin + imports
CRUD, CSV import with validate-before-commit behavior, optional XLSX, unlink/relink, bulk role sync and audit.

## Milestone 5 — Probation teams
Candidate CRUD/import, team CRUD, mentor many-to-many assignment, team role mapping/sync.

## Milestone 6 — Evaluations
Evaluation periods, configurable peer/mentor forms, Score/Text questions, open/close state machine, peer/mentor authorization, submissions, Core/Admin-only results and snapshots.

## Milestone 7 — Decisions
Bulk pass/fail command/API, promotion to Member, failure retention policy, Discord kick/role sync jobs, idempotency and audit. Add conflict/retry tests.

## Milestone 8 — Hardening
Integration tests with PostgreSQL (Testcontainers if suitable), authorization matrix tests, rate limiting for link/evaluation endpoints, structured logs, health checks, import limits, security review and deployment docs.

## Explicitly deferred
Events, registrations, check-in, attendance analytics, public website UI.

## Future milestone — Events & Attendance
1. Add Events domain/application boundary and lifecycle with Core/Admin-only management.
2. Add `EventPosition` with capacity and optional `RequiredDepartmentId`; no generic rule engine.
3. Add Member registration to a position as `Pending`, Core/Admin approval/rejection, and direct assignment with Department-rule bypass.
4. Implement `AllowMultiplePositions` and concurrency-safe hard capacity. No waitlist.
5. Add manual event-level attendance. Core/Admin may check in active Members without registration/assignment.
6. Add Member event/attendance history queries and Core/Admin event attendance reporting.
7. Add audit coverage and tests for lifecycle, authorization, eligibility, bypass, capacity races, duplicate registration, multiple-position policy and duplicate check-in.
