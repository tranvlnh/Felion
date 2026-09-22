# CI workflows

The repository CI workflow runs in `bot/` and:

1. starts PostgreSQL 16 as a service;
2. installs the locked npm dependency tree;
3. compiles the TypeScript bot; and
4. runs the Vitest suite.

The workflow does not connect to Discord or apply production migrations. Changes that
need a live guild still require the production Discord integration checklist in
[`docs/TODO.md`](../../docs/TODO.md).
