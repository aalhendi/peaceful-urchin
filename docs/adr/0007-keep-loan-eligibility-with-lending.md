# 0007: Keep loan eligibility with Lending

**Date:** 2026-09-29

**Status:** Accepted

## Context

The task asks us to show whether a customer can receive a loan and to block a customer from receiving one. It does not say who applies the block. We had two booleans: `loan_eligible` in Credit and `blocked` in Lending. Credit could say yes while Lending refused the loan. We have no separate credit assessment process or rule. AFAIK eligibility can grow to an independent service pulling data from many sources.

## Decision

The CINET loan block will be the single source of truth. Lending stores it and reports a customer as eligible when they are not blocked. Credit confirms that the customer exists but does not store or answer loan eligibility.

We chose to let authorized CINET staff set or clear the block through Lending. Delinquency does not change it automatically. When a bank creates a loan, Lending checks the block while holding the customer row lock in the same transaction that saves the loan. A customer without a block row is eligible until CINET blocks them.

Staff with `Customer.Read` can read eligibility. Staff with `Loan.Create` or `Loan.Block` can also check it because those operations need the answer. Only CINET staff with `Loan.Block` can change the block.

## Consequences

There is one current status to read and change. Lending still asks Credit about customer existence before creating a loan or changing a block.

We do not have an independent credit assessment (service/worker) yet. If we add one, we will name it separately and define how it relates to our master CINET block. Should probably supersede it.
