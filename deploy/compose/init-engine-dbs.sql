-- VertexBPMN engine databases.
-- postgres/init already creates `vertexbpmn_bpmn` from POSTGRES_DB;
-- create the remaining four engine databases idempotently.
SELECT 'CREATE DATABASE vertexbpmn_tenants'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'vertexbpmn_tenants')\gexec
SELECT 'CREATE DATABASE vertexbpmn_simulation'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'vertexbpmn_simulation')\gexec
SELECT 'CREATE DATABASE vertexbpmn_events'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'vertexbpmn_events')\gexec
SELECT 'CREATE DATABASE vertexbpmn_decision'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'vertexbpmn_decision')\gexec
