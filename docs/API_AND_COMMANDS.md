# API and Discord Command Contract

This is a planning contract, not generated OpenAPI. Codex should keep actual OpenAPI aligned.

## API v1
- `GET /api/v1/me`
- `GET/POST /api/v1/members`
- `GET/PATCH /api/v1/members/{id}`
- `POST /api/v1/members/{id}/unlink-discord`
- `POST /api/v1/members/{id}/sync-discord-roles`
- `POST /api/v1/imports/members` (multipart CSV; optional XLSX)
- `GET/POST /api/v1/probation/candidates`
- `POST /api/v1/probation/candidates/import`
- `GET/POST/PATCH /api/v1/probation/teams`
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
