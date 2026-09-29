CREATE TABLE customers (
    civil_id text PRIMARY KEY,
    name text NOT NULL
);

INSERT INTO customers (civil_id, name)
VALUES ('296051500019', 'Demo Customer One'),
       ('304022900002', 'Demo Customer Two');
