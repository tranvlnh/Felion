# Felion — Codex Starter Pack

This folder contains the repository-level context needed before Codex starts implementing **Felion**, the .NET 10 Discord-integrated club-management backend.

Start with `AGENTS.md`, then `docs/PRODUCT_SPEC.md`, `docs/ARCHITECTURE.md`, `docs/DATA_MODEL.md`, and `docs/IMPLEMENTATION_PLAN.md`.

The `.codex/skills/` directory contains project-specific skills for NetCord, domain rules and testing. These are intentionally concise; library APIs should still be verified against current official documentation.

## Recommended initial Codex task

> Read AGENTS.md and all docs. Bootstrap Milestone 0 and Milestone 1 only. Do not implement events/check-in. Use .NET 10, ASP.NET Core, EF Core PostgreSQL and tests. Before editing, report the proposed project tree and package choices. After editing, run format/build/test and report remaining blockers.

## Future-proofing
The specification now reserves an `Events & Attendance` module. Do not implement it in the first milestone, but preserve the module boundaries and contracts described in `docs/ARCHITECTURE.md` and `docs/DATA_MODEL.md`. This prevents Member/Discord/Probation code from becoming coupled to future event-position registration, Core/Admin approval/direct assignment, hard capacity, and manual attendance behavior.
