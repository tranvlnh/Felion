# Implementation Plan

## Pre-agent — Manual skeleton
The user creates `Felion.slnx`, the six approved source projects, the three approved test projects, and project references defined by ADR-013. The agent does not own this structural bootstrap.

## Milestone 0 — Foundation Setup
Validate the existing skeleton and dependency direction without reorganizing it. Configure central package management, nullable/analyzers, PostgreSQL EF provider, OpenAPI, health checks, DI/module registration conventions, configuration, logging/ProblemDetails, test infrastructure, CI, and development/secrets documentation. Do not implement business features in this milestone.

## Milestone 1 — Persistence + core domain
Implement Department, Generation, Member, ProbationCandidate, DiscordIdentityLink, AuditLog and migrations. Add unique constraints and normalization. Seed only configuration/reference data, never real secrets.

## Milestone 2 — Member admin + imports
Implement Member CRUD, create-only CSV/XLSX import with validate-before-commit behavior, row-level reports, temporary development authorization and audit coverage.

## Milestone 3 — Discord linking and role sync
Host NetCord in the same application. Implement verification button/modal, link use case, role mapping, role creation, role synchronization and privileged Discord commands. Add retryable DiscordSyncJob for side effects.

## Milestone 4 — Google Workspace authentication
Configure Google auth, resolve returned email to active Member, issue application principal with MemberId/Position claims, implement policies and `/me`. Reject unknown/inactive members even with valid Workspace-domain account.

## Milestone 5 — Probation teams
Candidate CRUD/import, team CRUD, mentor many-to-many assignment, team role mapping/sync.

## Milestone 6 — Evaluations
Fixed Open/Closed evaluation periods, fixed peer/mentor criteria, Discord-first submissions, application authorization, separate aggregates and Core/Admin-only raw results.

## Milestone 7 — Decisions
Bulk pass/fail command/API, promotion to Member, failure retention policy, immediate role synchronization, durable Discord kick retry, idempotency and audit. Add conflict/retry tests.

## Milestone 9 — Hardening
Integration tests with PostgreSQL (Testcontainers if suitable), authorization matrix tests, rate limiting for link/evaluation endpoints, structured logs, health checks, import limits, security review and deployment docs. Link/evaluation rate limits and cookie/HTTPS/response-header hardening are complete; remaining work is ingress/upload hardening and live integration coverage.

## Explicitly deferred
Registration, check-in, attendance analytics and public website UI.

## Milestone 8 — Events & Attendance
1. Add Events domain/application boundary and lifecycle with Core/Admin-only management.
2. Add `EventPosition` with capacity and optional `RequiredDepartmentId`; no generic rule engine.
3. Add Member registration to a position as `Pending`, Core/Admin approval/rejection, and direct assignment with Department-rule bypass.
4. Implement `AllowMultiplePositions` and concurrency-safe hard capacity. No waitlist.
5. Add manual event-level attendance. Core/Admin may check in active Members without registration/assignment.
6. Add Member event/attendance history queries and Core/Admin event attendance reporting.
7. Add audit coverage and tests for lifecycle, authorization, eligibility, bypass, capacity races, duplicate registration, multiple-position policy and duplicate check-in.
