# Product Specification — Phase 1

## Goal
Create a Discord-integrated club administration backend. The first release focuses on identity linking, Discord role synchronization, member management, probation management/evaluation and event operations, including manual attendance.

## Actors
- **Admin**: unrestricted administration.
- **Core**: manage members, probation, evaluations and (later) events.
- **Member**: authenticated club member; phase 1 mostly read-only/self-service foundations.
- **Probation candidate**: Discord-only identity; no website access.
- **Mentor**: an active Member assigned to one or more probation teams. Mentor is a relationship, not a position.

## Member lifecycle
Admin creates members manually or imports CSV/Excel. Required logical data: StudentId, FullName, ClubEmail, Department, Generation, Position. A member is active/inactive independently of Position.

Member management is available to active Core/Admin members. Import is create-only: a normalized StudentId or ClubEmail that already exists is a row error and never updates the existing Member. The complete file is validated before persistence; any row error prevents all rows from being imported. CSV and XLSX use the headers StudentId, FullName, ClubEmail, Department, Generation and Position. Department accepts its ID, slug or name; Generation accepts its ID, code or name.

Website sign-in uses Google Workspace. After Google authenticates the email, the application must find an active Member with the same normalized ClubEmail. Domain membership alone grants nothing.

## Discord linking
A verification message exposes a button. Button opens a Discord modal containing StudentId. On submit:
1. Normalize StudentId.
2. Find exactly one eligible Member or ProbationCandidate.
3. Reject if StudentId is already linked to another Discord user.
4. Reject if Discord user is already linked to another identity.
5. Persist link transactionally.
6. Synchronize configured Discord roles immediately from the current database state.
7. Audit the link.

To mitigate abusive retries, Discord linking is limited to five attempts per Discord user in each fixed ten-minute window. A rejected attempt must not reach identity lookup or persistence.

Admin/Core may unlink/relink and force role sync through web/API and Discord application commands according to authorization.

Role synchronization is executed immediately after the application persists a linking or administrative data change, using current database data rather than a job. `/role sync` applies the same operation to every active linked Member and ProbationCandidate. `DiscordSyncJob` is retained only for durable `KickUser` retries after FAIL; the worker must not hot-loop a failing kick job and schedules its next attempt with backoff.

## Discord role mapping
Mappings are configuration stored in DB, not hard-coded IDs. Supported role dimensions:
- Member position: Admin/Core/Member
- Probation base role
- Department
- Generation
- Probation team

Admin can choose an existing guild role or create a new Discord role and save the mapping. Only the configured single guild is valid.

Admin may also use the internal dashboard to create an official Member and assign multiple existing, non-managed guild roles to an active Member or ProbationCandidate. The assignment list is replacement-based: omitted roles are removed from Felion's managed set on the next synchronization, while automatic dimension mappings remain independent. The dashboard displays the effective union of per-person assignments and applicable automatic mappings, labeling automatic roles separately; editing still replaces only the per-person assignments. A person may be configured before linking; after joining the configured guild, entering StudentId in the Discord verification modal links the identity and synchronizes both automatic and per-person roles. PASS transfers per-person assignments to the new Member. FAIL removes the candidate's assignments.

The dashboard role catalog shows only existing roles that the bot can assign: non-managed roles below the bot's highest role and with effective `ManageRoles` or `Administrator` permission. If the bot has no assignable custom role, the catalog is empty rather than failing because the bot has no custom highest role.

When synchronizing a subject, an unrelated automatic mapping that is currently outside the bot's hierarchy must not block assignable roles for that subject. A role that is actually desired for the subject and cannot be assigned still fails synchronization with a permission/hierarchy error.

## Probation
Probation candidates are separate from Members. Each candidate belongs to exactly one Department, one Generation and optionally one active ProbationTeam. Active Core/Admin members may create and edit an active candidate's StudentId, FullName, Department and Generation; candidate status remains controlled only by the PASS/FAIL decision workflow. Changing the StudentId of a linked active candidate updates its Discord identity link in the same persistence operation.

A team has a name and optional/configured Discord role. A team may have many mentors. Mentors must reference active Members; a Member may mentor multiple teams.

## Evaluation
Evaluation is Discord-first. Admin creates an `EvaluationPeriod` and it opens immediately; only Admin may create or close periods. A period has `Open` or `Closed` status, and no scheduled lifecycle is used.

Peer evaluation is fixed to `Contribution`, `Communication` and `Attitude`, each an integer from 1 to 5, plus an optional note. An active probation candidate may evaluate another active candidate in the same team, never themself. The unique submission key is `(EvaluationPeriodId, EvaluatorCandidateId, TargetCandidateId)`; the evaluator may edit it while the period is Open.

Mentor evaluation is fixed to `Attendance`, `TaskCompletion` and `LearningInitiative`, each an integer from 1 to 10, plus an optional note. An active Member may evaluate candidates only in teams they mentor. Multiple mentors may evaluate one candidate, while each mentor has one submission per candidate and period that can be edited while Open.

Only Core/Admin can read raw submissions and aggregated results. Candidates cannot read evaluation results, and mentors cannot read peer evaluations or other mentors' submissions. Peer and mentor aggregates remain separate; there is no shared FinalScore, weighting, normalization or automatic PASS/FAIL.

Peer and mentor submissions snapshot the displayed identities so historical data remains understandable after candidate/member changes. Database unique constraints and application validation enforce duplicate, lifecycle, identity and team rules. Submission attempts are limited to ten per reviewer in each fixed one-minute window.

## Final decision
Core/Admin selects candidates in bulk and chooses PASS/FAIL.

PASS must be atomic from the application's perspective:
- validate candidate is eligible and linked state is consistent;
- create active Member using candidate identity fields, `Position=Member`, and a generated Workspace email;
- transfer Discord link;
- transfer per-person Discord role assignments;
- sync Discord roles: remove probation/team roles, add Member/Department/Generation mappings and retained per-person assignments;
- record immutable audit event;
- archive or delete probation record according to configured `Probation:SuccessPolicy` (`Archive` or `Delete`).

The generated Workspace email uses the normalized given name followed by the initials of the family and middle-name tokens, plus `Authentication:Google:WorkspaceDomain`; for example, `Trần Hữu Vinh` becomes `vinhth@gdscptit.dev`. A generated email conflict is reported as a per-candidate decision failure.

FAIL:
- record immutable audit event;
- remove per-person Discord role assignments;
- kick linked Discord user from guild (handle already-left idempotently);
- archive/delete probation record according to `ProbationFailurePolicy`.

`ProbationFailurePolicy`: `MarkInactive | Delete`. Audit records must never be lost. Evaluation history must remain interpretable even when candidate rows are deleted. A failed candidate's identity link is removed and the kick is queued for retry.

## Imports
Phase 1 supports manual CRUD and bulk import. CSV is mandatory. Excel (.xlsx) may be supported using a maintained library; import must validate the entire file and return row-level errors. Do not partially import by default.

## Audit
Audit privileged mutations at minimum: member create/update/import, link/unlink/relink, role mapping/create/sync, per-person role assignment changes, team/mentor changes, evaluation period creation/closure, peer/mentor submission create/update, pass/fail decisions and configuration changes.

Audit should capture: actor type/id, action, entity type/id where available, timestamp, correlation/request id, and JSON before/after or structured metadata without secrets.

# Events & Attendance Module
Event lifecycle, event-local positions, registration management and manual attendance are implemented in the Events module. The module boundary remains explicit, and Event must not be baked into Member or Discord entities.

## Authorization
Every active Member may view published events and their own registration/attendance history. Only Core/Admin may create, edit, cancel, publish, manage positions, approve/reject registrations, directly assign members, and check members in. Every Core can manage every event; `CreatedByMemberId` is audit metadata, not an ownership boundary.

## Event model
Events are voluntary club staffing activities. There is no required-event/target-audience/absence/excuse model. An Event can expose one or more staffing positions such as Technical Support, Media, Reception, etc. Positions are event-local names and are not club Departments.

An Event has `AllowMultiplePositions` (default false). When false, one Member may have at most one active/accepted staffing position in that Event. When true, the same Member may be accepted into multiple positions.

## Event positions and eligibility
Each `EventPosition` has its own capacity and eligibility. Phase-one eligibility is intentionally simple: optionally require membership in one Department. No generic expression/rule engine, Generation rule, Position rule, SQL, scripts, or arbitrary JSON predicates.

A normal Member may request a position only when eligible and capacity is still available. Registration never auto-accepts: it is submitted as Pending and Core/Admin decides. Core/Admin may directly assign any active Member to any position, bypassing the Department requirement. Direct assignment still obeys the event's multiple-position policy; capacity bypass must not be assumed unless a future requirement explicitly allows it.

`Pending`, `Approved` and `Assigned` registrations all reserve capacity. Once their total reaches capacity, do not accept a new registration request or assignment and do not create a waitlist. Rejection releases the reserved capacity.

## Registration
Registration represents a Member asking to staff a specific EventPosition. Approval/assignment is separate from the request. Preserve who approved/rejected/assigned and when. A Member cancelling their own request/assignment is allowed only when event lifecycle/policy permits; do not invent automatic reassignment.

## Attendance / check-in
Attendance is event-level, not position-level. `EventAttendance` is a historical fact that a Member appeared at the event. Initial check-in is manual only and can be performed by Core/Admin. Core/Admin may check in any active Member even if the Member never registered or was never assigned to a position.

Only one effective attendance record per `(EventId, MemberId)` is allowed. Store `CheckedInAt` and `CheckedInByMemberId`. Check-in is permitted while the Event is `InProgress` and after it is `Completed`, so Core/Admin can add omitted historical records; a repeated check-in returns the existing record and must not duplicate attendance or audit. No checkout, QR, absence, excuse, or required-participation semantics in the current scope. Attendance history must remain available for Member activity reporting and should survive Member deactivation.

## Lifecycle
Keep lifecycle explicit and small: `Draft -> Published -> RegistrationClosed -> InProgress -> Completed`, with `Cancelled` as a terminal alternative. Only Core/Admin may transition lifecycle. Published events are visible to Members. Registration availability is determined by lifecycle plus position capacity.

Material edits after registrations/approvals exist must be deliberate and audited. Do not silently invalidate accepted registrations by changing a position's Department requirement or capacity below current accepted occupancy.

## Member Department invariant
Every Member belongs to exactly one Department. `Admin` and `Core` always use the special `Core` Department. A regular `Member` must use a non-Core Department. Promotion to Admin/Core replaces the previous Department with `Core`; Felion does not retain the former Department as a separate field.
