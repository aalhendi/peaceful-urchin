INSERT INTO staff_user_roles (staff_user_id, role)
SELECT staff_users.id, 'PaymentWriter'
FROM staff_users
WHERE username = 'bank@example.test'
ON CONFLICT DO NOTHING;

INSERT INTO staff_user_roles (staff_user_id, role)
SELECT staff_users.id, 'CustomerReader'
FROM staff_users
WHERE username = 'cinet@example.test'
ON CONFLICT DO NOTHING;
