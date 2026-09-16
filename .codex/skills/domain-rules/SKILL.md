---
name: felion-domain-rules
description: Use when changing members, probation, teams, evaluations, promotion/failure decisions, authorization, or persistence schema.
---
# Felion domain rules

Read `AGENTS.md`, `docs/PRODUCT_SPEC.md`, and `docs/DATA_MODEL.md` first.

Key invariants:
- Member != ProbationCandidate.
- Mentor must be an active Member.
- Candidate has at most one team; Member may mentor many teams.
- Peer reviewer and target must be different active candidates in the same team.
- Mentor reviewer must mentor target's team.
- Only Open periods accept evaluation writes.
- Only Core/Admin can read evaluation results.
- Scores are advisory; Core/Admin manually decide pass/fail.
- Promotion creates a Member; it does not mutate a candidate into a Member row.
- Audit/evaluation history must survive retention deletion.
- Use DB constraints for uniqueness and transactions/idempotency for decisions.
