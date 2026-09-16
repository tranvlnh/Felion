# Felion — TODO

## Pre-agent — Manual skeleton (User)
- [x] Create `Felion.slnx`
- [x] Create `Felion.Host`, `Felion.Domain`, `Felion.Application`, `Felion.Infrastructure`, `Felion.Api`, `Felion.Bot`
- [x] Create Domain/Application/Integration test projects
- [x] Configure approved project references from ADR-013
- [x] Commit the manual skeleton

## Milestone 0 — Foundation Setup (Agent)
- [x] Verify approved solution structure/references without reorganizing it
- [x] Add central package management
- [x] Add formatting/analyzers
- [x] Add health checks and OpenAPI
- [x] Configure PostgreSQL/EF Core
- [x] Add module DI registration and configuration foundation
- [x] Add structured logging, correlation ID and ProblemDetails handling
- [x] Add NetCord Gateway integration foundation
- [x] Add test harness and integration smoke tests
- [x] Add CI restore/format/build/test workflow

## Milestone 1 — Core domain & persistence
- [x] Department + reserved `Core` department invariant
- [x] Generation
- [x] Member + Position/Status
- [x] ProbationCandidate
- [x] DiscordIdentityLink uniqueness
- [x] AuditLog
- [x] EF Core mappings/migrations

## Milestone 2 — Member import & management
- [x] Member CRUD API
- [x] CSV import
- [x] Excel import
- [x] Import validation/report

## Milestone 3 — Discord linking & roles
- [x] Verify button/modal
- [x] StudentId lookup/link via Discord transport
- [x] StudentId lookup/link application use case
- [x] Duplicate-link protection
- [x] Transactional link audit and DiscordSyncJob enqueue
- [x] Role mapping management for existing guild role IDs
- [x] Role creation
- [x] Position/Department/Generation/Probation/ProbationTeam role sync
- [x] Unlink/relink/force sync

## Milestone 4 — Web identity & authorization
- [x] Google Workspace OAuth
- [x] Active Member lookup
- [x] Admin/Core/Member policies
- [x] Probation web denial

## Milestone 5 — Probation teams
- [x] Team CRUD
- [x] Team Discord role mapping (existing `ProbationTeam` mapping and sync path)
- [x] Candidate assignment
- [x] Multi-mentor assignment

## Priority MVP — Probation Admin Dashboard
- [x] Static same-origin dashboard at `/admin/probation/`
- [x] Candidate list/detail with MSSV/name/Department/Generation/Team/Status filters
- [x] Candidate create/edit, atomic team change and audit/role-sync preservation
- [x] Team create/edit, mentor assignment/removal and destructive-action confirmations
- [x] PASS/FAIL confirmation UI backed by existing decision workflow
- [x] Candidate application/API/dashboard integration coverage

## Milestone 6 — Evaluation
- [x] EvaluationPeriod lifecycle
- [x] Configurable Score/Text form
- [x] Peer evaluation same-team rules
- [x] Mentor evaluation rules
- [x] Core/Admin-only results
- [x] Immutable/history-safe snapshots

## Milestone 7 — Probation decisions
- [x] Bulk PASS/FAIL
- [x] PASS promotion to Member
- [x] Discord sync job/retry
- [x] FAIL kick
- [x] MarkInactive/Delete policy
- [x] Preserve audit/evaluation history

## Milestone 8 — Events
- [x] Event CRUD/lifecycle
- [x] EventPosition + capacity definition
- [x] Department eligibility
- [x] `AllowMultiplePositions`
- [x] Member Pending registration
- [x] Core/Admin approve/reject
- [x] Core/Admin direct assignment with eligibility bypass
- [x] Concurrency-safe capacity enforcement
- [x] Manual event-level check-in without registration requirement
- [x] Member participation history

## Cross-cutting
- [x] Audit privileged Event, position, registration and attendance mutations
- [x] Authorization tests
- [x] Domain invariant tests
- [x] Scope-safe temporary actor middleware dependency resolution
- [x] Events API integration tests (authorization, check-in transport and Problem Details mapping)
- [x] PostgreSQL integration test for EventAttendance uniqueness under concurrent inserts
- [x] Shared fixed-window rate limits for Discord linking and peer/mentor evaluation submissions
- [x] HTTP `429`/`Retry-After` integration coverage for rate-limited evaluation submission
- [x] Cookie/HTTPS/HSTS, Kestrel and security response-header hardening
- [ ] API integration tests
- [x] Keep specs/status/TODO/decisions synchronized for completed Member management slice
