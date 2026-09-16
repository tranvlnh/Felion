---
name: felion-testing
description: Use when adding tests or completing any feature in this repository.
---
# Testing skill

For every feature, test business invariants and forbidden paths, not only happy paths.

Minimum high-value cases:
- duplicate StudentId/Discord link races are rejected;
- inactive/unknown Workspace email cannot access protected API;
- Member/Core/Admin policy matrix;
- probation cannot access web API;
- peer cannot self-review or review another team;
- mentor cannot review a team they do not mentor;
- closed/draft period rejects submissions;
- candidate cannot read evaluation results;
- pass creates Member exactly once and schedules correct Discord role sync;
- fail schedules kick and obeys retention policy;
- audit survives candidate deletion;
- role sync does not remove unrelated Discord roles.

Prefer unit tests for pure rules and PostgreSQL integration tests for constraints/transactions. Do not rely solely on EF InMemory for relational behavior.
