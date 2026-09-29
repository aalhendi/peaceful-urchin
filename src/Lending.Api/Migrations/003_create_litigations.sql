CREATE TABLE loan_litigations (
    id uuid PRIMARY KEY,
    loan_id uuid NOT NULL REFERENCES loans (id),
    court_id text NOT NULL,
    status text NOT NULL,
    verdict_date date,
    opened_by uuid NOT NULL,
    opened_at timestamp with time zone NOT NULL DEFAULT now(),
    verdict_by uuid,
    verdict_recorded_at timestamp with time zone,
    UNIQUE (loan_id, court_id)
);

CREATE INDEX loan_litigations_loan_idx ON loan_litigations (loan_id);
