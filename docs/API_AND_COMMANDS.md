# API and Discord Command Contract

This is a planning contract, not generated OpenAPI. Codex should keep actual OpenAPI aligned.

## API v1
- `GET /api/v1/auth/login` — start Google Workspace OAuth; optional local `returnUrl`
- `GET /api/v1/auth/me` — current active Member identity
- `POST /api/v1/auth/logout` — clear the Felion cookie session
- `GET/POST /api/v1/members`
- `GET/PATCH /api/v1/members/{id}`
- `POST /api/v1/members/{id}/unlink-discord`
- `POST /api/v1/members/{id}/relink-discord`
- `POST /api/v1/members/{id}/sync-discord-roles` (Core/Admin; synchronize roles immediately for an active linked Member)
- `POST /api/v1/imports/members` (multipart CSV; optional XLSX)
- `GET/POST /api/v1/probation/candidates`
- `GET/PATCH /api/v1/probation/candidates/{candidateId}`
- `PUT /api/v1/probation/candidates/{candidateId}/team`
- `POST /api/v1/probation/candidates/{candidateId}/sync-discord-roles` (Core/Admin; synchronize roles immediately for a linked active candidate)
- `GET /api/v1/probation/candidates/reference-data`
- `GET /api/v1/probation/candidates/mentor-options`
- `GET/POST/PATCH /api/v1/probation/teams`
- `GET /api/v1/probation/teams/{teamId}`
- `PUT/DELETE /api/v1/probation/teams/{teamId}/candidates/{candidateId}`
- `PUT/DELETE /api/v1/probation/teams/{teamId}/mentors/{memberId}`
- `GET/POST /api/v1/probation/evaluation-periods` (create is Admin-only and opens immediately)
- `POST /api/v1/probation/evaluation-periods/{id}/close` (Admin-only)
- `GET /api/v1/probation/evaluations/status` (Core/Admin only)
- `GET /api/v1/probation/evaluations/summary/{periodId}` (Core/Admin only)
- `GET /api/v1/probation/evaluations/{candidateId}` (Core/Admin only; raw submissions included)
- `POST /api/v1/probation/decisions` (bulk pass/fail)
- `GET/PUT /api/v1/discord/role-mappings`
- `POST /api/v1/discord/roles` (Admin; create guild role)
- `GET /api/v1/discord/role-assignments` (Admin; active Members and ProbationCandidates with individual assignments)
- `GET /api/v1/discord/role-assignments/roles` (Admin; assignable roles from the configured guild)
- `PUT /api/v1/discord/role-assignments/{subjectType}/{subjectId}` (Admin; replace individual role IDs, `subjectType=Member|Probation`, body `{ "discordRoleIds": ["123456789"] }`)
- `POST /api/v1/discord/sync`
- `GET /api/v1/audit` (Admin; Core if later desired)

## Discord UX
Verification should be component-first:
`#verify message -> [Link account] -> Modal(StudentId) -> ephemeral success/error`.

The current Bot adapter provides the verification message component payload and registers the button/modal handlers when `Discord:Token` and the configured `Discord:GuildId` are present. The linking handler delegates to the application use case; it does not authorize from Discord roles.

Suggested application commands:
- `/member info [user]`
- `/member unlink <user>`
- `/member sync <user>`
- `/probation info [user]`
- `/probation pass <user>`
- `/probation fail <user>`
- `/probation bulk` should generally redirect to web/API unless Discord UX is intentionally designed.
- `/team create <name>`
- `/team mentor add|remove ...`
- `/evaluation create <name>` (Admin; creates and opens a period)
- `/evaluation current`
- `/evaluation status`
- `/evaluation peer`
- `/evaluation mentor`
- `/evaluation view <candidateId> [periodName]` (Core/Admin)
- `/evaluation summary <periodName> [teamName]` (Core/Admin)
- `/evaluation close [periodName]` (Admin; confirmation component)
- `/role map ...`
- `/role create ...`
- `/role sync`

Implemented Discord administration commands:
- `/verification publish` publishes the verification message with the StudentId linking button in the current channel. Before the first link, it also accepts a Discord server Administrator for bootstrap; afterwards it accepts an active linked Felion Admin.
- `/role create name` creates a role in the configured guild and audits the mutation.
- `/role map kind subject role` maps an existing guild role. `kind` is `Position`, `Probation`, `Department`, `Generation` or `ProbationTeam`; `subject` is the display name for Department, Generation or ProbationTeam, and remains `Admin|Core|Member` or `Probation` for the fixed dimensions. The application stores the resolved canonical ID internally.
- `/role sync` synchronizes every active linked Member and ProbationCandidate from current database data; it is useful after changing mappings manually.
- `/department create name slug` creates a regular Department; `core` remains reserved for the seeded Core Department.
- `/generation create name code` creates a Generation with an uppercase canonical code.
- `/team create name` creates a probation team for an active linked Core/Admin.
- `/team panel` opens the interactive, ephemeral team administration embed.

Team Discord administration is available through `/team panel`. The ephemeral embed lets an
active linked Core/Admin select a team, create or rename/deactivate it, assign/remove active probation candidates,
and assign/remove active Member mentors. The candidate and mentor selectors use current database data and every
mutation calls the shared probation application service, so the one-team and active-mentor invariants remain in force.
Admin users can open `Cấu hình map role` from the same panel, choose a mapping dimension and subject, then select an
existing guild role through Discord's role menu. The selected role is checked against the bot's assignable role
catalog before the shared role-mapping use case persists it and synchronizes affected identities. The panel is
guild-scoped and ephemeral; its component IDs carry only stable IDs/keys and are re-authorized on every interaction.

These commands are registered guild-scoped for `Discord:GuildId`. Role, Department, Generation and role-mapping panel actions resolve the invoking Discord user through `DiscordIdentityLink` and require an active linked Member with Position `Admin`; team commands/panel actions require Position `Core` or `Admin`. Discord role possession alone is not sufficient. `/verification publish` has the documented first-link server-Administrator bootstrap exception. Responses are ephemeral.

Permissions must be resolved from linked DB Member position, not merely Discord role possession. Discord roles are presentation/access synchronization, not the source of truth for application authorization.

Google login issues an application cookie only when the normalized Google email matches an active `Member.ClubEmail`. Matching `WorkspaceDomain` alone never authorizes access, and probation candidates have no web principal. Member CRUD/import and Discord link management require `CoreOrAdmin`; Discord role mapping/creation requires `AdminOnly`. Development and Testing additionally accept `X-Felion-Actor-Member-Id` as a compatibility header after active-Member lookup; production rejects it. The import file is create-only and all-or-nothing. Required headers are `StudentId`, `FullName`, `ClubEmail`, `Department`, `Generation` and `Position`; Department accepts ID/slug/name and Generation accepts ID/code/name. Import responses contain `Committed`, `ImportedRows` and row-level `Errors`.

Discord role mapping management is Admin-only. `PUT /api/v1/discord/role-mappings` maps an existing positive Discord role ID using canonical keys: `Admin|Core|Member` for `Position`, `Probation` for the probation base role, and a non-empty GUID for `Department`, `Generation` or `ProbationTeam`. The Discord slash command resolves the latter dimensions from their names before calling the same application mapping use case.

Probation team management is Core/Admin-only. Team deactivation is a soft delete through `PATCH /api/v1/probation/teams/{teamId}` with `IsActive=false`; candidate assignment is limited to one active team, and mentors must be active Members.

Probation candidate management is Core/Admin-only. `GET /api/v1/probation/candidates` supports `page`, `pageSize`, `search` (StudentId/full name), `departmentId`, `generationId`, `teamId`, `hasTeam` and `status` filters. Create/edit accepts StudentId, FullName, DepartmentId and GenerationId; edit is limited to active candidates and never changes status. A linked candidate profile edit and `PUT /api/v1/probation/candidates/{candidateId}/team` synchronize roles immediately from current data. `POST /api/v1/probation/candidates/{candidateId}/sync-discord-roles` explicitly performs the same immediate synchronization for an active linked candidate. `reference-data` provides dropdown data, and `mentor-options` searches active Members for the team mentor selector. The internal dashboard is available at `/admin/probation/` and reuses the same-origin Google cookie session. Its Admin-only `Members & Discord roles` tab also provides the create-Member form, which calls the existing `POST /api/v1/members` contract; Member creation remains Core/Admin-authorized at the API boundary.

Evaluation uses fixed domain criteria and is Discord-first; there is no web Evaluation Dashboard or form builder. Admin creates/open periods through `/evaluation create`, and Admin closes them through a confirmation component. Discord evaluation commands accept the exact period/team display names (`periodName`/`teamName`, case-insensitive); ambiguous names are rejected. Peer modals collect Contribution, Communication and Attitude (1..5); mentor modals collect Attendance, TaskCompletion and LearningInitiative (1..10). Application authorization enforces same-team, mentor-team, no-self, one-submission and Open-only edit rules. Core/Admin can use the read-only API and `/evaluation view`/`summary`; candidate and mentor participants cannot read raw results.

Discord account linking is limited to five attempts per Discord user per ten-minute fixed window. Peer and mentor evaluation submissions are limited to ten attempts per reviewer per one-minute fixed window. HTTP rate-limit rejection returns `429 Too Many Requests` and a `Retry-After` header; Discord interactions return an ephemeral retry message.

`POST /api/v1/probation/decisions` accepts `{ "items": [{ "candidateId": "...", "decision": "Pass|Fail" }] }` and returns one outcome per candidate. PASS creates a regular Member with a generated email from the candidate's name and `Authentication:Google:WorkspaceDomain`, transfers the identity link and any individual Discord role assignments, then synchronizes the new Member's roles immediately. FAIL removes individual role assignments, queues an idempotent guild kick and removes the identity link. Retention is configured through `Probation:SuccessPolicy` (`Archive|Delete`) and `Probation:FailurePolicy` (`MarkInactive|Delete`).

`POST /api/v1/discord/roles` is Admin-only and creates a role in the configured guild. When the Discord adapter is configured, role mapping upserts verify the role belongs to that guild, store the role name returned by Discord and immediately synchronize every active linked identity from current database data. Member profile/status changes, probation candidate profile/team changes, link/relink operations and individual assignment changes also synchronize immediately through the same role synchronization path. `DiscordSyncJob` is retained only for durable `KickUser` retries after FAIL; role synchronization no longer creates or consumes jobs. Individual role assignments are Admin-only, reject `@everyone`, managed integration roles and roles the bot cannot assign. The role catalog filters out roles at or above the bot's highest role, so a bot without a custom highest role returns an empty assignable catalog instead of a hierarchy error. Individual assignments are included in the same immediate synchronization union as automatic mappings. During synchronization, an unrelated automatic mapping outside the bot's hierarchy is ignored for mutation; a desired role outside the hierarchy still returns a synchronization error. `GET /api/v1/discord/role-assignments` returns individual assignments and applicable automatic roles separately, allowing the dashboard to show all effective roles without persisting automatic mappings as per-person assignments.

# Events API / bot surface
The Event lifecycle, position, registration, attendance and Member history endpoints below are implemented. Registration cancellation remains planned.

- `POST /api/v1/events` — Core/Admin create draft
- `PATCH /api/v1/events/{eventId}` — Core/Admin edit according to lifecycle/invariants
- `POST /api/v1/events/{eventId}/publish`
- `POST /api/v1/events/{eventId}/registration/close`
- `POST /api/v1/events/{eventId}/start`
- `POST /api/v1/events/{eventId}/complete`
- `POST /api/v1/events/{eventId}/cancel`
- `GET /api/v1/events` — active Member sees published events
- `GET /api/v1/events/{eventId}` — includes positions and capacity
- `POST /api/v1/events/{eventId}/positions` — Core/Admin
- `PATCH /api/v1/events/{eventId}/positions/{positionId}` — Core/Admin
- `POST /api/v1/events/{eventId}/positions/{positionId}/registrations` — current Member requests eligible position; creates Pending
- `GET /api/v1/events/{eventId}/registrations` — Core/Admin
- `POST /api/v1/events/{eventId}/registrations/{registrationId}/approve` — Core/Admin
- `POST /api/v1/events/{eventId}/registrations/{registrationId}/reject` — Core/Admin
- `POST /api/v1/events/{eventId}/positions/{positionId}/assignments` — Core/Admin directly assign Member; Department eligibility bypassed
- `POST /api/v1/events/{eventId}/attendance` — Core/Admin manual check-in by `{ memberId }` while `InProgress` or `Completed`; registration not required and repeated requests are idempotent
- `GET /api/v1/events/{eventId}/attendance` — Core/Admin attendance reporting
- `GET /api/v1/members/{memberId}/event-history` — Member self, or any Member history for Core/Admin; includes registration and attendance facts

## Planned cancellation endpoint

- `DELETE /api/v1/events/{eventId}/positions/{positionId}/registrations/me` — cancel own registration when permitted

Discord commands must call the same application use cases, e.g. `/event create`, `/event position`, `/event register`, `/event approve`, `/event assign`, `/event checkin`, `/event attendance`. Do not duplicate capacity, eligibility, approval or attendance rules in NetCord handlers.
