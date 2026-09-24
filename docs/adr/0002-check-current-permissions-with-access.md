# 0002: Check current permissions with Access on protected requests

**Date:** 2026-09-24

**Status:** Accepted

## Context

A Bank A employee signs in and receives a credential. Later, an administrator removes a role that gave the employee `Loan.Create`. The employee then sends `CreateLoan` to Lending with the still-valid credential. Should Lending trust the permissions known at sign-in, or ask Access what the employee may do now?

[OWASP recommends checking permissions on every request](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html#validate-the-permissions-on-every-request), but leaves the source of those permissions open.

## Decision

For every protected Lending or Credit request, ask Access to validate the credential and return the actor's identity, institution, and effective permissions from current assignments. Access reads its authoritative store without a permission cache. The receiving service checks the permission required by the requested action and the applicable resource scope, such as a bank employee's institution. We accept this lookup so a committed role change affects the next request. The credential format remains a separate decision.

## Consequences

If the revocation commits before Access reads the assignments, the next `CreateLoan` request fails authorization. A change after that read cannot undo a request already in flight; the check and Lending's write are not one transaction.

Each protected request adds a network call and depends on Access being available. If Access cannot answer, Lending and Credit fail closed and report a dependency failure, not a permission denial. Cross-service tests must cover revocation on the next request, wrong institution scope, and Access unavailability.
