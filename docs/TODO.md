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

## Milestone 6 — Evaluation
- [ ] EvaluationPeriod lifecycle
- [ ] Configurable Score/Text form
- [ ] Peer evaluation same-team rules
- [ ] Mentor evaluation rules
- [ ] Core/Admin-only results
- [ ] Immutable/history-safe snapshots

## Milestone 7 — Probation decisions
- [ ] Bulk PASS/FAIL
- [ ] PASS promotion to Member
- [ ] Discord sync job/retry
- [ ] FAIL kick
- [ ] MarkInactive/Delete policy
- [ ] Preserve audit/evaluation history

## Milestone 8 — Events
- [ ] Event CRUD/lifecycle
- [ ] EventPosition + capacity
- [ ] Department eligibility
- [ ] `AllowMultiplePositions`
- [ ] Member Pending registration
- [ ] Core/Admin approve/reject
- [ ] Core/Admin direct assignment with eligibility bypass
- [ ] Concurrency-safe capacity enforcement
- [ ] Manual event-level check-in without registration requirement
- [ ] Member participation history

## Cross-cutting
- [ ] Audit privileged mutations
- [x] Authorization tests
- [x] Domain invariant tests
- [ ] PostgreSQL integration tests
- [ ] API integration tests
- [x] Keep specs/status/TODO/decisions synchronized for completed Member management slice
