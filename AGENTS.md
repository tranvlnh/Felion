# AGENTS.md — Felion

## Mission

Build a production-minded TypeScript Discord bot for one configured guild using
Node.js, discord.js, Drizzle ORM and PostgreSQL.

## Language and communication

- Communicate with the user in Vietnamese by default.
- Use English identifiers for source code, database objects, commands and configuration keys.
- Ask before making consequential assumptions about domain behavior, schema or authorization.

## Product rules

- One configured Discord guild; never infer the guild from incoming data.
- `Member` and `ProbationCandidate` are separate aggregates.
- One StudentId maps to at most one active Member or ProbationCandidate.
- One Discord user maps to at most one active Member or ProbationCandidate.
- Discord linking requires only StudentId.
- Positions are `Admin`, `Core` and `Member`; Probation is not a position.
- Department and Generation are separate dimensions. Generation stores only a name and is used as a Discord role tag.
- Discord role mappings are configurable for position, probation, department, generation and probation team.
- Mentors must be active Members and may mentor multiple teams.
- Evaluation score criteria are configurable per kind. Every submission has exactly one optional fixed text note.
- Candidates may evaluate other candidates in their team, never themselves. Mentors may evaluate candidates in teams they mentor.
- Raw evaluations are visible only to Core/Admin. Scores never decide PASS/FAIL automatically.
- PASS creates a Member, transfers identity, synchronizes roles and audits the transaction.
- FAIL audits, kicks the user and retains historical audit/evaluation data.
- Every privileged mutation creates an AuditLog.

## Engineering rules

- Keep Discord handlers thin; business rules belong in domain/application modules.
- Use async APIs for I/O and transactions for multi-record mutations.
- Enforce uniqueness and cross-aggregate identity rules in PostgreSQL as well as application code.
- Store timestamps in UTC and Discord snowflakes consistently as strings.
- Never commit Discord tokens, database passwords or other secrets.
- Do not add Events, web/API authentication or generic repositories to this target.

## Working procedure

1. Read `docs/PROJECT_STATUS.md`, `docs/TODO.md`, `docs/DECISIONS.md` and the relevant specification.
2. Inspect existing TypeScript code before changing it.
3. State affected modules, schema changes and tests in a short plan.
4. Implement the smallest testable vertical slice.
5. Run `npm run build` and `npm test -- --run` before finishing.
6. Update the checkpoint documents truthfully.

## Definition of done

Relevant code compiles, tests pass, migrations exist for schema changes, authorization
and audit behavior are covered, and documentation matches the bot-only target.
