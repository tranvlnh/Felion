# CI workflows

`ci.yml` runs at the repository root (npm workspaces) and:

1. installs the locked dependency tree for `backend/` and `frontend/`;
2. generates the Prisma client, compiles the backend and builds the frontend;
3. type-checks `backend/scripts` and `backend/tests`; and
4. runs the Vitest suite (pure logic, no database or Discord needed).

It does not connect to Discord, Google or a database, and does not apply migrations.
