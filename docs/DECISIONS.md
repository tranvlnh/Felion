# Felion — Approved Decisions

> Chỉ ghi các quyết định đã được người dùng xác nhận hoặc đã có trong specification. Agent không được tự biến assumption thành decision.

## ADR-001 — Modular Monolith
Felion là modular monolith .NET 10, một deployable service chứa ASP.NET Core Web API và NetCord bot, dùng PostgreSQL làm system of record. Không tách microservices nếu chưa được yêu cầu.

## ADR-002 — Member và Probation tách biệt
`ProbationCandidate` không phải `Member`. PASS tạo Member mới và chuyển Discord identity; FAIL kick Discord và xử lý candidate theo `ProbationFailurePolicy` (`MarkInactive` hoặc `Delete`). Audit/evaluation history phải được giữ.

## ADR-003 — Member Department
Mọi Member thuộc đúng một Department. `Admin` và `Core` luôn thuộc Department `Core`. Member thường phải thuộc Department khác `Core`. Khi promote lên Admin/Core, Department chuyển sang `Core`; không lưu ban cũ.

## ADR-004 — Discord linking
Một StudentId chỉ liên kết một Discord account và một Discord account chỉ liên kết một identity. Liên kết qua Discord modal chỉ cần StudentId. Core/Admin có thể unlink/relink và force role sync.

## ADR-005 — Discord role dimensions
Role Discord được map độc lập theo Position, Department, Generation, Probation và ProbationTeam. Role có thể chọn role sẵn có hoặc được Core/Admin yêu cầu bot tạo.

## ADR-006 — Probation teams & mentors
Candidate thuộc tối đa một team. Team có thể có nhiều mentor; mentor bắt buộc là Member. Candidate chỉ peer-review candidate khác cùng team. Form evaluation chỉ có `Score` và `Text`. Candidate không được đọc kết quả; Core/Admin xem và quyết định PASS/FAIL thủ công.

## ADR-007 — Event positions
Registration luôn nhắm tới `EventPosition`, không đăng ký trực tiếp Event. Một Event có nhiều position, mỗi position có capacity và có thể yêu cầu một Department. Phase đầu không dùng generic rule engine.

## ADR-008 — Event registration
Member đủ điều kiện gửi registration ở trạng thái Pending; mọi registration cần Core/Admin duyệt. Core/Admin có thể direct-assign và bypass Department eligibility. Capacity là hard limit, không waitlist. `AllowMultiplePositions` cấu hình theo Event, mặc định false.

## ADR-009 — Event permissions
Mọi Core và Admin đều quản lý được mọi Event. Không có EventManager ACL. `CreatedByMemberId` chỉ dùng provenance/audit.

## ADR-010 — Attendance
Attendance là bằng chứng Member thực sự xuất hiện, độc lập với registration/position. Check-in thủ công bởi Core/Admin và có thể check-in Member chưa đăng ký. Không checkout, QR, required-event, absence hay excuse trong scope hiện tại.

## ADR-011 — Web authentication
Web dùng Google Workspace OAuth. Có email đúng domain chưa đủ quyền: account phải map tới Member active trong DB. Probation không có web access.

## ADR-012 — Communication
Agent giao tiếp với người dùng bằng tiếng Việt. Code, identifier, namespace, DB/API/config key và Git/PR text dùng tiếng Anh. Requirement chưa rõ mà ảnh hưởng domain/schema/security/architecture phải hỏi người dùng trước.

## ADR-013 — Solution skeleton is user-owned
The solution skeleton is created manually by the user before the agent starts implementation. The approved projects are `Felion.Host`, `Felion.Domain`, `Felion.Application`, `Felion.Infrastructure`, `Felion.Api`, and `Felion.Bot`, plus Domain/Application/Integration test projects. `Felion.Host` is the only executable/composition root. `Felion.Api` and `Felion.Bot` remain separate class-library adapters. The agent must not collapse, split, rename, or reorganize these project boundaries without explicit approval.

Approved dependency direction:

```text
Felion.Host
├── Felion.Api
├── Felion.Bot
└── Felion.Infrastructure

Felion.Api ────────────> Felion.Application
Felion.Bot ────────────> Felion.Application
Felion.Infrastructure ─> Felion.Application
Felion.Application ────> Felion.Domain
```

`Felion.Domain` must not depend on ASP.NET Core, EF Core, NetCord, PostgreSQL, or Infrastructure.

## ADR-014 — Member import semantics and development actor boundary
Member import is create-only and validates the entire CSV/XLSX file before committing. A normalized duplicate StudentId or ClubEmail is a row-level error; no existing Member is updated and any error prevents all rows from being persisted. Web authentication now uses Google Workspace and issues application claims only for an active matching Member. Development and Testing may additionally use the temporary `X-Felion-Actor-Member-Id` compatibility header after active-Member lookup; production rejects that transport.

## ADR-015 — Probation promotion identity and retention configuration
PASS always creates a regular `Member` (`Position=Member`). The application generates `ClubEmail` from the candidate's normalized given name followed by the initials of preceding name tokens, appended to `Authentication:Google:WorkspaceDomain`; generated email collisions fail that candidate decision. PASS retention is configurable with `Probation:SuccessPolicy` (`Archive` or `Delete`, default `Archive`). FAIL continues to use `Probation:FailurePolicy` (`MarkInactive` or `Delete`, default `MarkInactive`).

## ADR-016 — Event registration capacity reservation
`Pending`, `Approved` and `Assigned` EventRegistration records all reserve one EventPosition capacity slot. A new normal registration or direct assignment is rejected when the total of these statuses reaches capacity; rejection (and later cancellation) releases its slot. This prevents implicit waitlists.

## ADR-017 — Event attendance lifecycle
Core/Admin may manually check in an active Member when an Event is `InProgress` or `Completed`; `Cancelled` and earlier lifecycle states reject check-in. This permits adding omitted attendance after completion. Attendance remains one immutable historical fact per `(EventId, MemberId)`: repeated check-in is idempotent and does not modify or duplicate the record.

## ADR-018 — Link and evaluation abuse limits
Discord linking is limited to five attempts per Discord user per fixed ten-minute window. Peer and mentor evaluation submissions are limited to ten attempts per reviewer per fixed one-minute window. The limiter is shared at the application boundary so it applies to every transport; HTTP rejection returns `429` with `Retry-After`, while Discord returns an ephemeral retry response. The initial implementation is process-local because Felion currently has one application process. Anti-forgery enforcement for cookie-authenticated JSON mutations is explicitly deferred.

## ADR-019 — Priority Probation Admin Dashboard MVP
The internal Probation Admin Dashboard is implemented as framework-free static HTML/CSS/ES modules under `Felion.Host/wwwroot/admin/probation`, served at `/admin/probation/`. It calls the existing same-origin REST API and reuses Google Workspace cookie authentication. No separate frontend project, SPA framework, CORS policy or new authentication strategy is introduced for this MVP.

## ADR-020 — One-shot initial Admin bootstrap command
To resolve the chicken-and-egg startup dependency where member creation requires a pre-existing Core/Admin actor, `Felion.Host` exposes the explicit one-shot CLI command `bootstrap-admin`. It may be run in any environment, including as a one-off deployment process, but refuses to run after any Member exists. It reads the initial Admin and Generation details from configuration, creates the Generation when needed, creates an active Admin associated with the reserved Core Department, and records audit logs using `AuditActorType.System` in the same persistence operation. Bootstrap never runs during normal web startup and is not exposed over HTTP. Deployment supplies `Bootstrap__*` values through its configuration/secret system and removes them after success.

## ADR-021 — Admin-only Discord administration commands
The Discord administration surface is limited to active linked Admin Members. Guild-scoped slash commands support creating Discord roles, mapping existing roles to the configured Felion dimensions, creating Departments and creating Generations. Authorization is resolved from `DiscordIdentityLink` and the active Member record; Discord role possession is not an authorization source. Every successful privileged mutation records an audit log with the Discord actor identity.

## ADR-022 — Admin-published verification message
For the first-link bootstrap, a Discord user with the server `Administrator` permission may publish the standard verification message in the current channel through `/verification publish`, even before that Discord user is linked to a Felion Admin. After the initial link, the command also remains available to active linked Admins. The command is restricted to the configured guild, reuses the existing button/modal StudentId flow, checks server permission through a Discord adapter, and audits the Discord message channel, ID and authorization mode after the message is successfully sent. This bootstrap exception grants no Felion application authorization; every other privileged administration command still requires an active linked Felion Admin.

## ADR-023 — Name-based Discord role mapping command input
The Discord `/role map` command accepts the display name of a Department, Generation or ProbationTeam and resolves it to the existing canonical GUID before persistence. Position and probation mappings keep their fixed keys (`Admin`, `Core`, `Member` and `Probation`). The HTTP role-mapping API remains canonical and continues to accept GUID keys. A missing or ambiguous entity name is rejected rather than guessed.
