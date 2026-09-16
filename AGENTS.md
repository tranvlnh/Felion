# AGENTS.md — Felion

## Mission
Build a production-minded modular monolith for one Discord guild using .NET 10, ASP.NET Core Web API, NetCord, EF Core and PostgreSQL (Supabase is only the managed PostgreSQL host).

## Language & Communication
- Communicate with the user in Vietnamese by default.
- Explanations, implementation plans, review summaries, questions, warnings, and progress reports must be written in Vietnamese.
- Source code must use English identifiers.
- Class, method, property, variable, namespace, database table/column, API route, command name, enum, and configuration key names must use English.
- Code comments should preferably be English.
- Git commit messages and pull request titles/descriptions should be English.
- Technical terms may remain in English when translating them would reduce clarity.
- If a requirement is ambiguous, conflicts with existing specifications, or materially affects architecture, schema, authorization, or domain behavior, ask the user in Vietnamese before making an assumption.
- Do not invent product behavior merely to keep implementation moving; explicitly request clarification when the decision is consequential.

## Non-negotiable product rules
- One Discord guild only. Guild ID is configuration, never inferred.
- `Member` and `ProbationCandidate` are separate aggregates/tables. Never model probation as a Member subtype/status.
- One student ID may link to at most one Discord user; one Discord user may link to at most one active Member/ProbationCandidate.
- Discord linking requires only StudentId. Do not invent OTP/email verification.
- Member data is created/imported by admins. Member includes club Workspace email.
- Website authentication uses Google Workspace. A matching domain alone NEVER authorizes access: email must match an active Member record.
- Positions: Admin, Core, Member. Probation is not a member position.
- Admin: full access. Core: member/probation/event management; every Core may manage every Event. Member: read published events, request eligible event positions, and view own history. Probation has no web access.
- Department and Generation are separate dimensions. A person belongs to exactly one Department and one Generation.
- Discord roles are configurable mappings for position/probation, department, generation and probation team. Admin can map an existing role or request role creation.
- A probation team has a name, Discord role, many candidates, and zero-or-more mentors. Mentors MUST be Members. A Member may mentor multiple teams.
- Weekly evaluation uses configurable periods/forms. Question types are ONLY Score and Text note.
- Peer evaluation: candidate may evaluate other candidates in the same team, never self. Mentor evaluation: mentor may evaluate candidates in teams they mentor.
- Raw evaluation/results are visible only to Core/Admin. Candidates cannot read them.
- Core/Admin manually decide pass/fail; scores never automatically pass/fail a candidate.
- PASS is a promotion transaction: create Member -> transfer Discord link -> sync roles (remove probation/team, add member + configured department/generation roles) -> audit -> archive/delete probation according to policy.
- FAIL: audit -> kick from guild -> archive/delete according to `ProbationFailurePolicy` (`MarkInactive` or `Delete`). Audit history must survive deletion. Evaluation history should survive candidate deletion via snapshots/nullable FK strategy.
- Every privileged mutation must produce an AuditLog.

## Engineering rules
- Architecture: modular monolith, not microservices and not generic repository/unit-of-work ceremony.
- Keep HTTP endpoints and NetCord handlers thin. Business rules belong in application/domain services shared by both transports.
- Use async APIs and CancellationToken end-to-end for I/O.
- Use EF Core migrations. PostgreSQL-specific behavior is acceptable.
- Store Discord snowflakes as `long`/`ulong` consistently; if PostgreSQL bigint is used, validate values fit signed bigint or use numeric mapping deliberately.
- Use UTC timestamps (`DateTimeOffset`) in persistence.
- Use optimistic/concurrency-safe operations for linking, promotion, pass/fail and evaluation submission. Database unique constraints are mandatory; app checks alone are insufficient.
- Never put Discord token, Google client secret or DB password in committed appsettings. Use environment variables/user-secrets.
- Authorization is server-side. UI visibility is never considered authorization.
- Avoid cascade deletes that destroy audit/evaluation history.
- Add tests for domain invariants and authorization before considering a feature complete.

## Working procedure for Codex
1. Read `docs/PRODUCT_SPEC.md`, `docs/ARCHITECTURE.md`, `docs/DATA_MODEL.md`, and the relevant skill before coding.
2. For library/API details that may have changed, consult official docs. Do not guess NetCord APIs.
3. Before a feature, state the affected module(s), schema/API changes and tests in a short plan.
4. Implement the smallest vertical slice that compiles and is testable.
5. Run `dotnet format --verify-no-changes`, `dotnet build`, and `dotnet test` before finishing when available.
6. Do not silently implement Events/Attendance before its milestone. Preserve its boundary: Event has staffing positions; registration targets a position and requires Core/Admin approval; attendance is manual and event-level.
7. If requirements conflict, these repo instructions and `docs/PRODUCT_SPEC.md` are authoritative unless the user explicitly overrides them.

## Definition of done
- Build passes with warnings treated seriously.
- Migration exists for persistence changes.
- Validation + authorization + audit are implemented.
- Unit/integration tests cover happy path and key forbidden/conflict paths.
- Public API changes are reflected in OpenAPI and docs.

## Extensibility guardrails
Felion is a modular monolith intended to gain modules such as Events & Attendance. Preserve explicit module boundaries even though all modules deploy in one process and may share one PostgreSQL database.
- Do not put business logic in controllers, Minimal API handlers, NetCord handlers, EF configurations, or hosted workers.
- Do not access another module's DbSet/repository directly from application code. Use its application contract/query port.
- Do not create a generic repository, generic workflow engine, generic rule scripting system, or premature microservices.
- Members are the stable official-person identity for club features. Future Events references MemberId but cannot mutate Member state.
- Registration and attendance/check-in are separate concepts.
- Event eligibility must be typed/allowlisted and extensible; never persist executable SQL/C#/scripts.
- Check-in mechanisms are strategies behind one application use case; QR/manual/admin must share validation, idempotency and audit.
- Historical audit/evaluation/attendance data must not cascade-delete with Member or Probation rows.

## Planned Events invariants
- Events are voluntary; there is no required-event, target-audience, absence or excuse model.
- An Event has one or more event-local staffing positions. Each position has hard capacity and optionally requires one Department.
- Normal Member registration creates Pending; Core/Admin approve/reject. Core/Admin may directly assign and bypass Department eligibility.
- `AllowMultiplePositions` is event-configurable and defaults false. Capacity has no waitlist.
- Every Core/Admin can manage every Event; do not create EventManager ACLs.
- Attendance is a unique event-level record `(EventId, MemberId)`, manual by Core/Admin, and does not require prior registration/assignment. No checkout.
- Preserve attendance as historical Member activity and audit privileged mutations.

## Agent Checkpoint Protocol

### Before starting work
Read in this order:
1. `AGENTS.md`
2. `docs/PROJECT_STATUS.md`
3. `docs/TODO.md`
4. `docs/DECISIONS.md`
5. Only the specification documents relevant to the current task
6. Inspect existing code before proposing changes

Do not start implementation until the current checkpoint and approved decisions are understood.

### During work
- Work on one coherent task/vertical slice at a time.
- Do not silently change approved decisions or module boundaries.
- Do not turn assumptions into architecture/domain decisions.
- If ambiguity affects domain behavior, schema, authorization, security, or architecture, stop and ask the user in Vietnamese.
- Keep scope tight; do not perform unrelated refactors.

### Before finishing a session
1. Run formatting for changed code.
2. Run `dotnet build`.
3. Run relevant tests.
4. Update `docs/TODO.md` truthfully.
5. Update `docs/PROJECT_STATUS.md` as the current save point, not as an append-only diary.
6. Update `docs/DECISIONS.md` only for decisions explicitly approved by the user or already established by specification.
7. State the next recommended task in Vietnamese.

Never mark work complete when relevant build/tests are failing. Never claim a command was run if it was not run.

## Definition of Done
A task may be checked complete only when its relevant implementation compiles, tests pass, authorization/domain validation is covered, required audit behavior is implemented, and affected specification/checkpoint files are synchronized. Do not leave temporary TODO/FIXME placeholders in completed production paths unless explicitly documented as remaining work.

## Documentation Drift Rule
When code changes domain behavior, update the relevant specification in the same task. If the change represents a newly approved decision, also update `docs/DECISIONS.md`. Then update `docs/TODO.md` and `docs/PROJECT_STATUS.md` before ending the session.


## Approved Solution Skeleton

The user creates the solution/project skeleton manually before agent implementation. Treat the project boundaries and references in `docs/DECISIONS.md` ADR-013 and `docs/ARCHITECTURE.md` as approved constraints. `Felion.Host` is the only executable/composition root. `Felion.Api` and `Felion.Bot` remain separate class-library adapters. Do not rename, merge, split, or reorganize these projects without explicit user approval. Milestone 0 for the agent is Foundation Setup on the existing skeleton, not project creation.
