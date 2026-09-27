# 0004: Use the same service layers and parse once

**Date:** 2026-09-26

**Status:** Accepted

## Context

We want to move between Access, Lending, and Credit without relearning where code lives, even while a service is small.
From past experience with layered architectures, I have run into the same field in the web request, the domain type, and the database changeset. The limits disagreed. We want the useful separation without repeating business validation at every layer.

## Decision

Each service uses `Web`, `Application`, `Domain`, and `Infrastructure` inside its API project. `Program.cs` wires them together. `Web` handles incoming HTTP and its concerns, such as decoding requests, size limits, and responses. How services communicate with each other is a separate decision.

`Domain` owns value objects, entities, and their stable rules. Values are parsed through private construction paths and expose read-only state. Application operations take those types, not the original strings or numbers. `Application` coordinates the operation and checks facts that can change, such as current permissions or loan eligibility. It decides which steps must succeed together within its service. `Infrastructure` runs SQL, manages the database transaction, and maps stored rows back into domain values. An invalid stored row is an integrity failure.

PostgreSQL still enforces primary keys, foreign keys, required fields, uniqueness, and constraints needed for concurrent writes.

## Consequences

The shared layout costs a few files in small services but gives each kind of code a predictable home. A parsed value carries its rule forward; current state must still be checked when the operation runs. For example, Lending must check a customer's loan block in the same transaction that creates the loan.

We need to keep C# constructors and properties closed enough that callers cannot bypass parsing. When a database constraint also protects a domain rule, tests should catch disagreement. We will add types and interfaces for real operations, without building a generic framework around them.
