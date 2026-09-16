# Felion — Project Status

Last Updated: 2026-09-16

## Current Milestone
Milestone 9 — Hardening (in progress)

## Current Focus
Priority MVP work đã hoàn tất: Probation Admin Dashboard nội bộ tại `/admin/probation/`, bao gồm candidate/team/mentor management và PASS/FAIL confirmation. Milestone 9 Hardening vẫn là milestone đang thực hiện; task MVP này không đánh dấu hoàn tất các phần Hardening còn lại.

## Completed
- [x] Product scope baseline
- [x] Modular-monolith architecture baseline
- [x] Member / Department / Generation rules
- [x] Discord linking and role-sync requirements
- [x] Probation / team / mentor / evaluation requirements
- [x] Event / position / registration / attendance requirements
- [x] Authorization baseline
- [x] Audit requirement
- [x] Agent checkpoint protocol
- [x] Approved solution skeleton and dependency direction verified
- [x] Central package management and build analyzers
- [x] ASP.NET Core ProblemDetails, exception/status handling and correlation ID
- [x] Structured JSON logging and OpenAPI foundation
- [x] PostgreSQL/EF Core registration and readiness health check
- [x] Conditional NetCord Gateway registration and readiness health check
- [x] Integration test harness and CI build/test workflow
- [x] Core domain entities and invariants for Department, Generation, Member and ProbationCandidate
- [x] DiscordIdentityLink global uniqueness model
- [x] AuditLog model with JSON metadata and non-cascading member actor reference
- [x] EF Core PostgreSQL mappings, Core department seed and initial migration
- [x] Domain and persistence-model tests
- [x] Member CRUD application service and API
- [x] Create-only CSV/XLSX import with all-or-nothing validation/report
- [x] Member mutation audit coverage
- [x] Temporary Development/Testing actor authorization boundary
- [x] Discord link application use case with StudentId normalization and active identity lookup
- [x] Duplicate Discord link protection with database uniqueness and race-safe conflict mapping
- [x] Transactional Discord link audit and retry-job enqueue model
- [x] Discord role mapping domain, Admin-only application service and API
- [x] Discord role mapping persistence constraints and migration
- [x] Discord verification button/modal transport and StudentId linking flow
- [x] Discord role creation/existence adapter and retryable synchronization worker
- [x] Discord unlink/relink/force sync with audit and queued role jobs
- [x] Google Workspace OAuth with application cookie session
- [x] Active Member lookup from normalized Workspace email
- [x] Admin/Core/Member authorization policy matrix
- [x] Probation web denial
- [x] Authentication and authorization tests
- [x] Probation team aggregate and mentor relationship
- [x] Probation team CRUD and soft deactivation
- [x] Candidate assignment/removal with one-team invariant and Discord role-sync enqueue
- [x] Multi-mentor assignment/removal with active-Member validation
- [x] Probation team persistence migration and model constraints
- [x] Probation team API and audit coverage
- [x] EvaluationPeriod Draft/Open/Closed lifecycle
- [x] Configurable Peer/Mentor Score/Text evaluation forms and questions
- [x] Evaluation persistence migration, validation, authorization and audit coverage
- [x] Peer/mentor submission rules, Open-only writes and duplicate edit behavior
- [x] Core/Admin-only raw results and history-safe identity/question snapshots
- [x] Bulk PASS/FAIL decision API with per-candidate outcomes
- [x] PASS promotion to regular Member with generated Workspace email
- [x] Transactional identity transfer, audit and role-sync enqueue
- [x] FAIL identity removal and retryable idempotent Discord kick
- [x] Configurable PASS/FAIL retention policies
- [x] Promotion/failure domain, application and Discord worker tests
- [x] Scope-safe temporary actor middleware dependency resolution
- [x] Event aggregate lifecycle and Core/Admin event management API
- [x] EventPosition persistence with optional Department eligibility and capacity definition
- [x] Event/event-position audit and domain/application/persistence-model tests
- [x] Pending-capacity reservation, registration approval/rejection and direct assignment
- [x] Department eligibility, multiple-position policy and optimistic-concurrency capacity enforcement
- [x] Event registration persistence/API/audit and migration
- [x] Manual event-level check-in for active Members without registration requirement
- [x] Check-in for `InProgress` and `Completed` events with idempotent duplicate handling
- [x] Event attendance persistence, audit, API/reporting and migration
- [x] Member event participation history with self/Core/Admin authorization
- [x] Events API integration tests for authorization, request binding, correlation ID and conflict Problem Details
- [x] PostgreSQL EventAttendance uniqueness race test with isolated migrated database
- [x] Shared rate limiting for Discord linking and peer/mentor evaluation submission
- [x] Rate-limit application and HTTP `429`/`Retry-After` integration coverage
- [x] Cookie/HTTPS/HSTS, Kestrel and security response-header hardening
- [x] Priority Probation Admin Dashboard MVP: static same-origin UI, candidate CRUD/filter/detail, atomic team change, team/mentor management and PASS/FAIL confirmation
- [x] Candidate management application/API authorization, validation, audit and dashboard static-shell coverage

## In Progress
Milestone 9 — Hardening còn lại (ingress/upload hardening và mở rộng live integration coverage). Priority Probation Admin Dashboard MVP đã hoàn tất ngoài milestone này.

## Known Issues / Open Questions
Không có blocker code. PostgreSQL và Discord Gateway được cấu hình optional để local build/test không cần secret hoặc service đang chạy. Google OAuth credentials phải được cung cấp qua environment variables hoặc user-secrets khi chạy thật. `X-Felion-Actor-Member-Id` chỉ là compatibility path cho Development/Testing và bị từ chối ngoài hai environment này. PostgreSQL EventAttendance integration test cần biến `FELION_POSTGRES_TEST_CONNECTION`; CI đã cấp PostgreSQL service, còn local không cấu hình sẽ skip test này. Chưa có live Discord integration test vì môi trường không có guild/token. Rate limiter hiện không chia sẻ state giữa nhiều replica; cần shared store nếu chuyển sang multi-instance. Anti-forgery cho cookie-authenticated JSON mutation được hoãn theo quyết định đã chốt. Khi triển khai sau reverse proxy, phải allowlist các proxy tin cậy trước HTTPS redirection và đặt `AllowedHosts` thành hostname production; không tin cậy `X-Forwarded-*` từ mọi client. EF CLI migration scaffolding đã sinh migration team/mentor và evaluation thành công; các migration được nhận diện bởi EF model.

## Next Recommended Task
Milestone 9 — Hardening: giới hạn upload import, sau đó mở rộng live integration coverage (PostgreSQL/Discord khi có hạ tầng).

## Verification
- dotnet restore: PASS — `dotnet restore Felion.slnx -p:NuGetAudit=false`
- dotnet format: PASS — `dotnet format Felion.slnx --verify-no-changes --no-restore`
- dotnet build: PASS — Release, 0 warnings, 0 errors
- dotnet test: PASS — 23 domain tests, 73 application tests, 29 standard integration tests; PostgreSQL integration test skip khi `FELION_POSTGRES_TEST_CONNECTION` chưa cấu hình
- NuGet vulnerability audit: PASS — không phát hiện package vulnerable, kể cả transitive dependencies
- EF migration: PASS — `20260916112125_AddEvaluations`
- EF migration: PASS — `20260916115048_AddEvaluationSubmissions`
- EF migration: PASS — `20260916123701_AddKickUserSyncOperation`
- EF migration: PASS — `20260916132229_AddEvents`
- EF migration: PASS — `20260916133349_AddEventRegistrations`
- EF migration: PASS — `20260916135416_AddEventAttendance`
- dotnet ef database update: PASS — local PostgreSQL đã apply `20260916133349_AddEventRegistrations`
- dotnet ef database update: PASS — local PostgreSQL đã apply `20260916135416_AddEventAttendance`
- dotnet ef database update: PASS — local PostgreSQL đã apply `20260916132229_AddEvents`
- dotnet ef database update: PASS — local PostgreSQL với user `postgres`, đã apply toàn bộ migration hiện tại
- PostgreSQL integration test: PASS — `ConcurrentAttendanceInsertsLeaveExactlyOneRecord` với database `felion_test_*` cô lập
