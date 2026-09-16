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

## ADR-014 — Member import semantics before web authentication
Member import is create-only and validates the entire CSV/XLSX file before committing. A normalized duplicate StudentId or ClubEmail is a row-level error; no existing Member is updated and any error prevents all rows from being persisted. Until Google Workspace authentication is implemented, Development and Testing use the temporary `X-Felion-Actor-Member-Id` header, while production rejects that temporary transport.
