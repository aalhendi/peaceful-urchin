CREATE TABLE customers (
    civil_id text PRIMARY KEY,
    loan_eligible boolean NOT NULL
);

INSERT INTO customers (civil_id, loan_eligible)
VALUES ('180010100006', true),
       ('304022900002', false);
