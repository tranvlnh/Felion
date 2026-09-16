# Felion — Project Status

Last Updated: 2026-09-16

## Current Milestone
Milestone 6 — Evaluation

## Current Focus
Milestone 5 đã hoàn tất. Save point tiếp theo là Evaluation với EvaluationPeriod lifecycle và configurable Score/Text form.

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

## In Progress
Không có. Milestone 5 đã hoàn tất.

## Known Issues / Open Questions
Không có blocker code. PostgreSQL và Discord Gateway được cấu hình optional để local build/test không cần secret hoặc service đang chạy. Google OAuth credentials phải được cung cấp qua environment variables hoặc user-secrets khi chạy thật. `X-Felion-Actor-Member-Id` chỉ là compatibility path cho Development/Testing và bị từ chối ngoài hai environment này. Migration mới chưa apply vào Development database vì local PostgreSQL từ chối role `felion`; PostgreSQL live integration test vẫn chưa có. Chưa có live Discord integration test vì môi trường không có guild/token. EF CLI migration scaffolding đã sinh migration team/mentor thành công; các migration trước đó và migration mới đều được nhận diện bởi EF model.

## Next Recommended Task
Milestone 6 — Evaluation: EvaluationPeriod lifecycle và configurable Score/Text form.

## Verification
- dotnet restore: PASS — `dotnet restore Felion.slnx -p:NuGetAudit=false`
- dotnet format: PASS — `dotnet format Felion.slnx --verify-no-changes --no-restore`
- dotnet build: PASS — Release, 0 warnings, 0 errors
- dotnet test: PASS — 11 domain tests, 39 application tests và 13 integration/import-model/transport/authorization tests
- dotnet ef database update: BLOCKED — local PostgreSQL báo `role "felion" does not exist`
