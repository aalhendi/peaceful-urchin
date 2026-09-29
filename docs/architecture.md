# Architecture and domain language

## What runs today

```mermaid
flowchart LR
    staff["Bank and CINET staff"] -->|Login, check actor, manage roles| access["Access API"]
    staff -->|Create loans, manage court cases, block loans, check eligibility, upload payments, read loan totals, repayments and credit grades| lending["Lending API"]
    staff -->|Read customer profiles and update names| credit["Credit API"]

    lending -->|Validate session and get current permissions| access
    credit -->|Validate session and get current permissions| access
    lending -->|Confirm customer exists| credit

    subgraph postgres["PostgreSQL server"]
        accessDb[(access_db)]
        lendingDb[(lending_db)]
        creditDb[(credit_db)]
    end

    access --> accessDb
    lending --> lendingDb
    credit --> creditDb
```

On protected requests, Lending and Credit ask Access who the current actor is. They then enforce their own permissions and resource scope.

When a bank creates a loan, Lending asks Credit whether the customer exists. Lending then checks the CINET loan block in the same transaction that saves the loan. [ADR 7](adr/0007-keep-loan-eligibility-with-lending.md) explains why Lending owns this status.

## Terms we use

| Term | Meaning in this demo |
| --- | --- |
| Institution | A bank or CINET. Access gives it a stable ID, a code, a display name, and a kind. A staff user belongs to one institution. |
| Customer | A person identified by a Kuwaiti Civil ID. |
| Loan eligibility | Lending's current answer to whether an existing customer can receive a new loan: yes when CINET has not blocked them. |
| Loan block | The customer-wide restriction changed by authorized CINET staff. A blocked customer cannot receive a new loan. |
| Loan | A bank's financing record for a customer. It records the principal, financing rate, tenor, start date, status, and reported repayment schedule. |
| Principal | The amount originally financed, in KWD. It is not the amount still due. |
| Financing rate | The reported interest or profit rate. Lending stores it but does not derive installment amounts from it. |
| Tenor | The length of the loan in months. |
| Repayment schedule / installment | The bank's expected payments: one installment per month for the loan's tenor. Each installment has a due date and a positive KWD amount. |
| Payment | Money the bank reports receiving against one of its loans. A bank's payment reference identifies a report so an identical retry does not create another payment. |
| Court case | A court reference recorded against one loan. Pending -> current. Innocent or Guilty is a dated verdict. The customer and bank come from the loan. |
| Active loan total | The count and original principal sum of started, open loans without a pending court case. A bank sees its own loans while CINET sees all banks' loans. |
| Delinquent loan | An open loan with an overdue amount greater than zero. An installment becomes overdue the day after its due date. Payments cover the oldest unpaid installments first, including future ones.|
| Next due payment | The oldest unpaid installment across the customer's visible open loans, including an installment already overdue. |
| Credit grade | A customer-wide A, B, C, or F result calculated from loans, overdue payments, and court verdicts. A pending case can instead return PendingReview. |

## Credit grade

Lending calculates the grade when an authorized staff member asks for it. Banks and CINET can read the grade across all banks, but a bank cannot use the grade endpoint to read another bank's loans or court cases. The response contains only the grade and the date used for the calculation.

For the grade and active-loan total, a loan becomes active on its start date while its status is Open. A loan created for a future start date does not count yet.

F takes priority when more than three open loans are delinquent, or when a loan has a recent Guilty verdict. A verdict remains recent for three years when the loan's original principal is **greater than** KWD 10,000; for KWD 10,000 or less, it remains recent for one year. The window ends on the verdict's anniversary.

Otherwise, a pending court case returns PendingReview. Without one, one to three delinquent loans give C. With no delinquencies, at least one open loan gives A; zero open loans gives B. The task gives no letter grade for a pending case, so PendingReview makes that gap visible instead of guessing.
