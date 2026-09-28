INSERT INTO staff_user_roles (staff_user_id, role)
SELECT id, 'LoanBlocker'
FROM staff_users
WHERE username = 'cinet@example.test'
ON CONFLICT DO NOTHING;
