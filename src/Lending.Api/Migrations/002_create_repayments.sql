CREATE TABLE loan_installments (
    loan_id uuid NOT NULL REFERENCES loans (id),
    sequence_no integer NOT NULL,
    due_date date NOT NULL,
    amount_kwd numeric NOT NULL,
    PRIMARY KEY (loan_id, sequence_no),
    UNIQUE (loan_id, due_date)
);

CREATE TABLE loan_payments (
    id uuid PRIMARY KEY,
    loan_id uuid NOT NULL REFERENCES loans (id),
    institution_id uuid NOT NULL,
    external_reference text NOT NULL,
    payment_date date NOT NULL,
    amount_kwd numeric NOT NULL,
    recorded_at timestamp with time zone NOT NULL DEFAULT now(),
    UNIQUE (institution_id, external_reference)
);

CREATE INDEX loan_payments_loan_date_idx ON loan_payments (loan_id, payment_date DESC, id DESC);
