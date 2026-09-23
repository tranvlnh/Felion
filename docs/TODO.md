# Felion TODO

Items marked complete are implemented in the current TypeScript bot. Items marked
pending may exist in the schema or product specification but are not yet exposed as a
working Discord workflow.

## Foundation and persistence

- [x] TypeScript runtime, configuration validation, and PostgreSQL client
- [x] Drizzle schema and migrations `0000`–`0004`
- [x] Remove web/API, browser authentication, and Events from the target
- [x] Preserve cross-aggregate StudentId and Discord-link uniqueness in the schema
- [x] Docker deployment and CI build/test workflow
- [x] Feature-owned Discord handlers and first-match interaction router
- [x] Typed actor authorization resolvers and shared application-error responses
- [x] Shared database and transaction types
- [x] Node-native `#app/*` alias for cross-module TypeScript imports
- [x] Evaluation application boundary with a feature-specific Drizzle persistence adapter

## Current administration flows

- [x] Initial Admin bootstrap
- [x] StudentId-based Discord linking with audit logging
- [x] Regular Member creation with Department/Generation lookup
- [x] Department creation
- [x] Name-only Generation creation
- [x] Department and Generation edit/deactivate behavior
- [x] Discord role mapping storage and administration
- [x] Configurable evaluation criterion add/reactivate, rename, and deactivate
- [x] Domain tests for normalization and evaluation invariants

## Remaining product work

- [x] Discord role synchronization and explicit assignment reconciliation
- [x] Probation team administration
- [x] Probation candidate creation, team assignment, and lifecycle management
- [x] Mentor assignment with active-Member validation
- [x] Evaluation period open/close commands
- [x] Peer and Mentor evaluation submission commands/components
- [x] Core/Admin-only raw evaluation views and full-period CSV reports
- [ ] Manual PASS workflow: create Member, transfer identity, synchronize roles, audit
- [ ] Manual FAIL workflow: audit, enqueue/kick Discord user, retain history
- [ ] Kick retry worker and operational retry visibility
- [ ] Production Discord integration checks
- [ ] Database-backed integration tests for transactional workflows
- [ ] Authorization and audit tests for every privileged mutation
