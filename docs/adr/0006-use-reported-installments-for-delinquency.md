# 0006: Use reported installments to decide when a loan is late

**Date:** 2026-09-28

**Status:** Accepted

## Context

The task asks for a next due payment and delinquent loans. A loan's amount, rate, and tenor do not tell us what is due on a particular date. We could calculate installments, but that would mean choosing a financing formula the task does not give us.

## Decision

The bank sends a schedule with the loan: one positive KWD installment per month, starting the month after the loan starts, for the length of its tenor. Lending saves the loan and schedule together. We keep the reported rate, but do not calculate the schedule from it.

Bank staff upload payments for their own loans. We reject a payment dated before its loan starts. A reference is unique within a bank. Repeating it with the same loan, date, and amount returns the first payment ID; changing any of those details is a conflict. The whole batch commits or rolls back. The customer comes from the stored loan; the authenticated bank must own that loan.

As of today's date in Kuwait (UTC+3), payments dated on or before today cover the oldest unpaid installments first, including future installments. An installment becomes late the day after it is due. An open loan with any overdue amount counts once as delinquent. "Next due" is the oldest unpaid installment, even if it is already late.

## Consequences

We can answer the repayment questions without a loan calculator. We do not check whether the reported principal and rate explain the schedule, or recalculate it after an early payment. A payment beyond the full schedule is stored, but we do not report a separate surplus balance or refund.

We answer for the current date, not from a preserved historical snapshot. Corrections, reversals, and disputes need their own workflow.
