CREATE TABLE institutions (
    id uuid PRIMARY KEY,
    code text NOT NULL UNIQUE,
    name text NOT NULL,
    kind text NOT NULL
);

CREATE TABLE staff_users (
    id uuid PRIMARY KEY,
    username text NOT NULL UNIQUE,
    password_hash text NOT NULL,
    institution_id uuid NOT NULL REFERENCES institutions (id),
    active boolean NOT NULL DEFAULT true
);

CREATE TABLE staff_user_roles (
    staff_user_id uuid NOT NULL REFERENCES staff_users (id),
    role text NOT NULL,
    PRIMARY KEY (staff_user_id, role)
);

CREATE TABLE staff_sessions (
    token_digest text PRIMARY KEY,
    staff_user_id uuid NOT NULL REFERENCES staff_users (id),
    expires_at timestamp with time zone NOT NULL,
    revoked_at timestamp with time zone
);

CREATE INDEX staff_sessions_user_idx ON staff_sessions (staff_user_id);
