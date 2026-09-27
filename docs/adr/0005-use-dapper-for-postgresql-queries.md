# 0005: Use Dapper for PostgreSQL queries

**Date:** 2026-09-26

**Status:** Accepted

## Context

[ADR 0003](0003-write-postgresql-migrations-in-sql.md) covers schema changes. We also need a way to query those schemas. EF Core would give us an object model and generated SQL, but that is more machinery than we want to understand and maintain here. Plain Npgsql keeps the SQL visible, though mapping every row and parameter by hand would become repetitive as the services grow.

Dapper keeps the SQL in our hands and maps simple parameters and rows. It does not give us SQLx-style compile-time query checks from Rust. I am not sure if anything in the C# ecosystem does at this time.

## Decision

Write queries in SQL and execute them with Dapper on Npgsql connections. Use this pattern across the services and in the migration runner. Npgsql owns connections and database transactions; Dapper executes the commands and maps their results.

Keep database rows separate from domain values. `Infrastructure` maps a row into the domain type through its parsing path, so Dapper cannot bypass a domain invariant just by filling properties.

## Consequences

We have one query style and can read the SQL that PostgreSQL will run. Dapper saves routine mapping code without adding EF's model or change tracker.

Column names, C# mappings, and SQL can still drift. Query errors appear at runtime, so tests that exercise real PostgreSQL matter. We accept that tradeoff and keep Npgsql available for connection handling and PostgreSQL operations Dapper does not cover.
