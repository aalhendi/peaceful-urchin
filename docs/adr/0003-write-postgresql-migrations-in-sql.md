# 0003: Write PostgreSQL migrations in SQL

**Date:** 2026-09-25

**Status:** Accepted

## Context

Each service has its own PostgreSQL database, and we need a repeatable way to change its schema. [EF migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) were an option. But the generated C# operations and model snapshot are ugly for this job. We would still need to understand the SQL, only now it is wrapped in C#.

[DbUp](https://dbup.readthedocs.io/en/latest/) and [Flyway](https://documentation.red-gate.com/fd/migrations-271585107.html) both let us write SQL. DbUp runs inside .NET and is the closest alternative to our runner. Flyway has a more complete migration tool, but would add a separate executable to our deployment. Both are reasonable choices. For the small set of rules we need now, we chose to keep the runner in this repository and make its behavior explicit.

## Decision

Each service keeps numbered PostgreSQL `.sql` files for its own database. A shared .NET runner applies pending files at startup. It records each file's version, name, and SHA-256 checksum in `__SchemaMigrations`, takes an advisory lock, and runs pending files in order inside one transaction. If an applied file is missing or has changed, startup fails.

Migrations only go forward. During development, the database is disposable, we can change the initial SQL and recreate it. Once we need to keep its data, changes to an applied schema get a new migration.

## Consequences

We can review schema changes as SQL. C# mappings and SQL can drift apart, this is an acceptable tradeoff.

Startup needs a reachable database and permission to change its schema. Concurrent starts wait for the lock. A failed run rolls back, but there is no automatic rollback after a migration succeeds. The single transaction also rules out operations such as `CREATE INDEX CONCURRENTLY`.

If we need more involved migrations or want the API to run without schema privileges, move migrations to a separate deployment step and reconsider DbUp or Flyway instead of growing this runner indefinitely.
