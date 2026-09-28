-- NOTE(aalhendi): Demo passwords are bank-local for bank@example.test and cinet-local for cinet@example.test.
INSERT INTO institutions (id, code, name, kind)
VALUES (uuidv7(), 'BANK_A', 'Bank A', 'Bank'),
       (uuidv7(), 'CINET', 'CINET', 'CINET')
ON CONFLICT (code) DO NOTHING;

WITH demo_users (username, password_hash, institution_code, roles) AS (
    VALUES
        ('bank@example.test',
         'AQAAAAIAAYagAAAAEMeagZKME7HWX2DCyWmFxvTi15RacCUXRuXXJ+odFbad1fHeW6n3qYGPJD87mw8cSA==',
         'BANK_A', ARRAY['CustomerReader', 'LoanCreator']),
        ('cinet@example.test',
         'AQAAAAIAAYagAAAAECq2T05rtkV4mCyKehkwLFD9kpm3bEC0tVF4shF+B0jsuqexphxxg5KG8CY3y6fNyA==',
         'CINET', ARRAY['CreditAnalyst', 'AccessAdmin'])
), inserted AS (
    INSERT INTO staff_users (id, username, password_hash, institution_id)
    SELECT uuidv7(), demo_users.username, demo_users.password_hash, institutions.id
    FROM demo_users
    JOIN institutions ON institutions.code = demo_users.institution_code
    ON CONFLICT (username) DO NOTHING
    RETURNING id, username
)
INSERT INTO staff_user_roles (staff_user_id, role)
SELECT inserted.id, role.code
FROM inserted
JOIN demo_users USING (username)
CROSS JOIN LATERAL unnest(demo_users.roles) AS role(code);
