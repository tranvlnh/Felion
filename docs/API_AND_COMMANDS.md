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
- `POST /api/v1/members/{id}/sync-discord-roles`
- `POST /api/v1/imports/members` (multipart CSV; optional XLSX)
- `GET/POST /api/v1/probation/candidates`
- `POST /api/v1/probation/candidates/import`
- `GET/POST/PATCH /api/v1/probation/teams`
- `GET /api/v1/probation/teams/{teamId}`
- `PUT/DELETE /api/v1/probation/teams/{teamId}/candidates/{candidateId}`
- `PUT/DELETE /api/v1/probation/teams/{teamId}/mentors/{memberId}`
- `GET/POST /api/v1/probation/evaluation-periods`
- `POST /api/v1/probation/evaluation-periods/{id}/open`
- `POST /api/v1/probation/evaluation-periods/{id}/close`
- `POST/PATCH /api/v1/probation/evaluation-forms`
- `POST /api/v1/probation/evaluations/peer`
- `POST /api/v1/probation/evaluations/mentor`
- `GET /api/v1/probation/evaluation-periods/{id}/results` (Core/Admin only)
- `POST /api/v1/probation/decisions` (bulk pass/fail)
- `GET/PUT /api/v1/discord/role-mappings`
- `POST /api/v1/discord/roles` (Admin; create guild role)
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
- `/evaluation open|close <period>`
- `/role map ...`
- `/role create ...`
- `/role sync [user]`

Permissions must be resolved from linked DB Member position, not merely Discord role possession. Discord roles are presentation/access synchronization, not the source of truth for application authorization.

Google login issues an application cookie only when the normalized Google email matches an active `Member.ClubEmail`. Matching `WorkspaceDomain` alone never authorizes access, and probation candidates have no web principal. Member CRUD/import and Discord link management require `CoreOrAdmin`; Discord role mapping/creation requires `AdminOnly`. Development and Testing additionally accept `X-Felion-Actor-Member-Id` as a compatibility header after active-Member lookup; production rejects it. The import file is create-only and all-or-nothing. Required headers are `StudentId`, `FullName`, `ClubEmail`, `Department`, `Generation` and `Position`; Department accepts ID/slug/name and Generation accepts ID/code/name. Import responses contain `Committed`, `ImportedRows` and row-level `Errors`.

Discord role mapping management is Admin-only. `PUT /api/v1/discord/role-mappings` maps an existing positive Discord role ID using canonical keys: `Admin|Core|Member` for `Position`, `Probation` for the probation base role, and a non-empty GUID for `Department`, `Generation` or `ProbationTeam`.

Probation team management is Core/Admin-only. Team deactivation is a soft delete through `PATCH /api/v1/probation/teams/{teamId}` with `IsActive=false`; candidate assignment is limited to one active team, and mentors must be active Members.

Evaluation period and form management is Core/Admin-only. A period transitions `Draft -> Open -> Closed`; only `Open` accepts evaluation submissions. Forms specify `Peer` or `Mentor` reviewer type and contain at least one ordered `Score` or `Text` question. Score questions use `ScoreMin`/`ScoreMax` (defaulting to `Evaluation:DefaultScoreMin`/`Evaluation:DefaultScoreMax`), while Text questions use `TextMaxLength` (defaulting to `Evaluation:DefaultTextMaxLength`). Question order is unique within a form. Period/form mutations are audited. `POST /api/v1/probation/evaluations/mentor` accepts an active Member's mentor submission and returns only a receipt; peer submission remains available through the shared application contract because probation candidates have no web access. `GET /api/v1/probation/evaluations/results` is Core/Admin-only and is the only current API surface that returns raw answers and reviewer/target identity snapshots. Re-submission while Open edits the existing reviewer/target/form submission.

`POST /api/v1/probation/decisions` accepts `{ "items": [{ "candidateId": "...", "decision": "Pass|Fail" }] }` and returns one outcome per candidate. PASS creates a regular Member with a generated email from the candidate's name and `Authentication:Google:WorkspaceDomain`, transfers the identity link and queues role synchronization. FAIL queues an idempotent guild kick and removes the identity link. Retention is configured through `Probation:SuccessPolicy` (`Archive|Delete`) and `Probation:FailurePolicy` (`MarkInactive|Delete`).

`POST /api/v1/discord/roles` is Admin-only and creates a role in the configured guild. When the Discord adapter is configured, role mapping upserts verify the role belongs to that guild and store the role name returned by Discord; the synchronization worker consumes retryable `DiscordSyncJob` rows and only mutates roles managed by Felion.

# Planned Events API / bot surface
These are contracts to preserve architecture; do not implement until the Events milestone is requested.

- `POST /api/v1/events` — Core/Admin create draft
- `PATCH /api/v1/events/{eventId}` — Core/Admin edit according to lifecycle/invariants
- `POST /api/v1/events/{eventId}/publish`
- `POST /api/v1/events/{eventId}/registration/close`
- `POST /api/v1/events/{eventId}/start`
- `POST /api/v1/events/{eventId}/complete`
- `POST /api/v1/events/{eventId}/cancel`
- `GET /api/v1/events` — active Member sees published events
- `GET /api/v1/events/{eventId}` — includes positions, capacity and current member state
- `POST /api/v1/events/{eventId}/positions` — Core/Admin
- `PATCH /api/v1/events/{eventId}/positions/{positionId}` — Core/Admin
- `POST /api/v1/events/{eventId}/positions/{positionId}/registrations` — current Member requests eligible position; creates Pending
- `DELETE /api/v1/events/{eventId}/positions/{positionId}/registrations/me` — cancel own registration when permitted
- `GET /api/v1/events/{eventId}/registrations` — Core/Admin
- `POST /api/v1/events/{eventId}/registrations/{registrationId}/approve` — Core/Admin
- `POST /api/v1/events/{eventId}/registrations/{registrationId}/reject` — Core/Admin
- `POST /api/v1/events/{eventId}/positions/{positionId}/assignments` — Core/Admin directly assign Member; Department eligibility bypassed
- `POST /api/v1/events/{eventId}/attendance` — Core/Admin manual check-in by MemberId; registration not required
- `GET /api/v1/events/{eventId}/attendance` — Core/Admin
- `GET /api/v1/members/{memberId}/event-history` — Member self or Core/Admin according to authorization policy

Discord commands must call the same application use cases, e.g. `/event create`, `/event position`, `/event register`, `/event approve`, `/event assign`, `/event checkin`, `/event attendance`. Do not duplicate capacity, eligibility, approval or attendance rules in NetCord handlers.
