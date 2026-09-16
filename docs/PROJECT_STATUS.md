# Felion — Project Status

Last Updated: 2026-09-16

## Current Milestone
Milestone 2 — Member import & management

## Current Focus
Milestone 2 đã hoàn tất. Milestone 3 chưa bắt đầu.

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

## In Progress
Không có. Milestone 2 đã hoàn tất.

## Known Issues / Open Questions
Không có blocker. PostgreSQL và Discord Gateway được cấu hình optional để local build/test không cần secret hoặc service đang chạy. `ProbationCandidate.TeamId` hiện là nullable scalar; bảng/team foreign key sẽ được hoàn thiện ở Milestone 5 khi module Probation Teams được triển khai. Chưa chạy database update hoặc PostgreSQL live integration test vì môi trường hiện không có database/role Felion. Google Workspace authentication chưa triển khai; Member API chỉ nhận temporary actor header trong Development/Testing.

## Next Recommended Task
Milestone 3 — Discord linking & roles: verification button/modal, linking, role mappings and retryable synchronization.

## Verification
- dotnet restore: PASS — `dotnet restore Felion.slnx`
- dotnet format: PASS — `dotnet format Felion.slnx --verify-no-changes --no-restore`
- dotnet build: PASS — Release, 0 warnings, 0 errors
- dotnet test: PASS — 6 domain tests, 5 application tests và 7 integration/import-model tests
