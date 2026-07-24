\set ON_ERROR_STOP on

SELECT 'CREATE ROLE knightonline_app LOGIN'
WHERE NOT EXISTS (
    SELECT 1
    FROM pg_catalog.pg_roles
    WHERE rolname = 'knightonline_app'
)
\gexec

\echo Set a password for the KnightOnline application role:
\password knightonline_app

SELECT 'CREATE DATABASE knightonline_dev OWNER knightonline_app'
WHERE NOT EXISTS (
    SELECT 1
    FROM pg_catalog.pg_database
    WHERE datname = 'knightonline_dev'
)
\gexec
