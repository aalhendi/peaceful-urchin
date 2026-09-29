CREATE TABLE customer_loan_blocks (
    customer_civil_id text PRIMARY KEY,
    blocked boolean NOT NULL DEFAULT false
);

-- NOTE(aalhendi): Seed one demo block as initial state.
INSERT INTO customer_loan_blocks (customer_civil_id, blocked)
VALUES ('304022900002', true);

CREATE TABLE loan_block_changes (
    id uuid PRIMARY KEY,
    customer_civil_id text NOT NULL REFERENCES customer_loan_blocks (customer_civil_id),
    blocked boolean NOT NULL,
    changed_by uuid NOT NULL,
    changed_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE INDEX loan_block_changes_customer_idx ON loan_block_changes (customer_civil_id, changed_at DESC);

CREATE TABLE loans (
    id uuid PRIMARY KEY,
    customer_civil_id text NOT NULL REFERENCES customer_loan_blocks (customer_civil_id),
    institution_id uuid NOT NULL,
    start_date date NOT NULL,
    tenor_months integer NOT NULL,
    amount_kwd numeric NOT NULL,
    rate_percent numeric NOT NULL,
    status text NOT NULL
);

CREATE INDEX loans_customer_idx ON loans (customer_civil_id);
CREATE INDEX loans_institution_idx ON loans (institution_id);
