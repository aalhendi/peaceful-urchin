INSERT INTO staff_user_roles (staff_user_id, role)
SELECT staff_users.id, demo_roles.role
FROM staff_users
JOIN (VALUES
    ('bank@example.test', 'LitigationReader'),
    ('cinet@example.test', 'LitigationReader'),
    ('cinet@example.test', 'LitigationWriter')
) AS demo_roles(username, role) ON demo_roles.username = staff_users.username
ON CONFLICT DO NOTHING;
