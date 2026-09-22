# Project

You have been assigned to design and develop a set of minimalistic services to simulate a microservice environment for managing loan and credit information. Find below the details of each data point managed:

### Loan Information

* Loan ID
* Customer
* Institution Name
* Loan start date
* Tenor
* Amount
* Rate
* Status – (Open, Closed)

### User

* Username
* Institution
* Roles

### Payment Ledger

* Payment Date
* Loan ID
* Customer ID
* Institution id
* Amount Paid

### Customer

* Customer CID
* Customer name
* DOB
* Customer Loan Eligibility status

### Litigation

* Court ID
* Load ID
* Institution
* Customer
* Status – (Innocent, Guilty, Pending)
* Date of Verdict

---

Scope and define the microservices as you see fit and draw a diagram of the relationship between the services.

The following functions should be possible within your overall architecture:

* Get Customer Information
* Get Customer Delinquent Loans
* Get Total of a customer’s loans where the account is active and is not currently in legal state.
* Get Customer Next Due Payment
* Get Last 5 customer payments
* Change user roles to allow read/write on customer data
* Block user from receiving a loan
* Upload user’s recent payments
* Create user loan
* Calculate Credit Score grade
* **A:** 1 or More active loans with no delinquencies
* **B:** 0 active loans, with no delinquencies
* **C:** Between 1-3 delinquencies, no loans in litigation
* **F:** More than 3 delinquencies, or 1 or more loans with guilty litigation less than 3 years old if amount of loan > 10000, or guilty litigation less than 1 years old if amount < 10000
* Grading can be based on a timed job if it would create delays in processing



---

### Deliverables:

* Minimum of 3 microservices within a single Github Repo
* Open Api Documentation + Readme with Setup Instructions (Be sure to detail any dependencies)
* Architecture Diagram
* Any relevant SQL migrations/seed data
* Test user credentials (1 credit bureau user and 1 Bank user)

---

### Restrictions:

* Tech stack: .Net Core + Postgresql
* Each service containerized
* Docker Compose (or equivalent in Podman)
* Use of Claude Code/Copilot/Opencode/etc. heavily discouraged. If used, that should be clearly indicated.
* Comments only where design decisions are made, do not over comment.

---

### Rubric:

* Completeness and Correctness
* Clarity of documentation and design decisions
* Standardization of output and naming
* Ability to run the service architecture without the need for deviation from the Readme
* Following standard security for APIs (Blocking Sql injection, Xss, etc.)
* Implementation of Authentication and Authorization (Not basic auth)
