# 0008: Rewrite our migrations before 1.0

**Date:** 2026-09-29

**Status:** Accepted

## Context

As mentioned in [ADR 0003](0003-write-postgresql-migrations-in-sql.md), our runner checks each migration's checksum and refuses to start if an applied file changes. That protects a database whose data we need to keep. During development, we own these schemas and can recreate the databases. Adding a new migration for every correction to a schema we have not released would leave us with a long history of experiments.

## Decision

Until the 1.0 production release, we can edit, combine, or remove SQL migrations for databases we created and own. If a changed migration has already run locally, we reset that development database and apply the current files from scratch. We do not bypass the checksum check.

This only applies while the data is disposable. If a database has data we need to preserve, or the schema belongs to another system, we use a new forward migration. Once 1.0 is in production, applied migrations stay as they are and every schema change gets a new forward migration.

## Consequences

A fresh database gets a small, readable migration history that describes the schema we want to ship. Local data and seed changes are recreated when we reset. We check the rewritten migrations against a fresh database before treating the change as done.
