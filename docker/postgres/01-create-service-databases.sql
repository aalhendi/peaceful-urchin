CREATE ROLE access_app LOGIN PASSWORD 'access-local';
CREATE DATABASE access_db OWNER access_app;
REVOKE CONNECT ON DATABASE access_db FROM PUBLIC;

CREATE ROLE lending_app LOGIN PASSWORD 'lending-local';
CREATE DATABASE lending_db OWNER lending_app;
REVOKE CONNECT ON DATABASE lending_db FROM PUBLIC;

CREATE ROLE credit_app LOGIN PASSWORD 'credit-local';
CREATE DATABASE credit_db OWNER credit_app;
REVOKE CONNECT ON DATABASE credit_db FROM PUBLIC;
