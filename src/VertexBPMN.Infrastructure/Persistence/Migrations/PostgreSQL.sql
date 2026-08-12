-- VertexBPMN Consolidated DDL for PostgreSQL (CLEANED)
-- Primary target database. Uses JSONB for collections and dictionaries.
-- Adjust schema if needed: CREATE SCHEMA IF NOT EXISTS vertexbpmn; SET search_path TO vertexbpmn;

-- Optional cleanup (commented out for safety)
-- DROP TABLE IF EXISTS process_mining_events, simulation_scenarios, decision_instances, dmn_decision_tables,
--   decision_definitions, users, multi_instance_execution, incident, history_event, tasks, job, variable,
--   execution_token, process_instance, process_definition, engine_deployment, tenants CASCADE;

CREATE TABLE engine_deployment (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    tenant_id TEXT
);
CREATE INDEX IF NOT EXISTS ix_engine_deployment_created_at ON engine_deployment(created_at);
CREATE INDEX IF NOT EXISTS ix_engine_deployment_tenant ON engine_deployment(tenant_id);
CREATE INDEX IF NOT EXISTS ix_engine_deployment_name ON engine_deployment(name);

CREATE TABLE process_definition (
    id UUID PRIMARY KEY,
    key TEXT NOT NULL,
    name TEXT NOT NULL,
    version INT NOT NULL,
    bpmn_xml TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    deployment_id UUID NOT NULL REFERENCES engine_deployment(id) ON DELETE CASCADE,
    tenant_id TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_process_definition_key_version ON process_definition(key, version);
CREATE INDEX IF NOT EXISTS ix_process_definition_key ON process_definition(key);
CREATE INDEX IF NOT EXISTS ix_process_definition_tenant ON process_definition(tenant_id);
CREATE INDEX IF NOT EXISTS ix_process_definition_deployment ON process_definition(deployment_id);

CREATE TABLE process_instance (
    id UUID PRIMARY KEY,
    process_definition_id UUID NOT NULL REFERENCES process_definition(id) ON DELETE RESTRICT,
    business_key TEXT,
    tenant_id TEXT,
    started_at TIMESTAMPTZ NOT NULL,
    ended_at TIMESTAMPTZ,
    state TEXT NOT NULL,
    instance_id TEXT NOT NULL,
    process_id TEXT NOT NULL,
    status INT NOT NULL,
    active_tasks JSONB NOT NULL DEFAULT '[]',
    active_tokens JSONB NOT NULL DEFAULT '[]',
    variables JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL,
    last_modified TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_process_instance_definition ON process_instance(process_definition_id);
CREATE INDEX IF NOT EXISTS ix_process_instance_business_key ON process_instance(business_key);
CREATE INDEX IF NOT EXISTS ix_process_instance_tenant ON process_instance(tenant_id);
CREATE INDEX IF NOT EXISTS ix_process_instance_state ON process_instance(state);
CREATE INDEX IF NOT EXISTS ix_process_instance_started_at ON process_instance(started_at);

CREATE TABLE execution_token (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id) ON DELETE CASCADE,
    current_node_id TEXT NOT NULL,
    node_type TEXT NOT NULL,
    variables JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL,
    assigned_worker TEXT,
    assigned_at TIMESTAMPTZ,
    retry_count INT NOT NULL DEFAULT 0,
    state TEXT
);
CREATE INDEX IF NOT EXISTS ix_execution_token_instance ON execution_token(process_instance_id);
CREATE INDEX IF NOT EXISTS ix_execution_token_current_node ON execution_token(current_node_id);
CREATE INDEX IF NOT EXISTS ix_execution_token_state ON execution_token(state);
CREATE INDEX IF NOT EXISTS ix_execution_token_assigned_worker ON execution_token(assigned_worker);
CREATE INDEX IF NOT EXISTS ix_execution_token_created_at ON execution_token(created_at);

CREATE TABLE variable (
    id UUID PRIMARY KEY,
    scope_id UUID NOT NULL,
    name TEXT NOT NULL,
    type TEXT NOT NULL,
    value TEXT,
    tenant_id TEXT,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_variable_scope ON variable(scope_id);
CREATE INDEX IF NOT EXISTS ix_variable_name ON variable(name);
CREATE INDEX IF NOT EXISTS ix_variable_type ON variable(type);
CREATE INDEX IF NOT EXISTS ix_variable_tenant ON variable(tenant_id);
CREATE INDEX IF NOT EXISTS ix_variable_instance ON variable(process_instance_id);

CREATE TABLE job (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id) ON DELETE CASCADE,
    type TEXT NOT NULL,
    due_date TIMESTAMPTZ NOT NULL,
    retries INT NOT NULL,
    error_message TEXT,
    tenant_id TEXT,
    state TEXT NOT NULL,
    payload TEXT
);
CREATE INDEX IF NOT EXISTS ix_job_instance ON job(process_instance_id);
CREATE INDEX IF NOT EXISTS ix_job_type ON job(type);
CREATE INDEX IF NOT EXISTS ix_job_state ON job(state);
CREATE INDEX IF NOT EXISTS ix_job_due_date ON job(due_date);
CREATE INDEX IF NOT EXISTS ix_job_tenant ON job(tenant_id);

CREATE TABLE tasks (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    type TEXT NOT NULL,
    assignee TEXT,
    tenant_id TEXT,
    created_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ,
    due_date TIMESTAMPTZ,
    form_key TEXT,
    form_schema TEXT,
    last_modified TIMESTAMPTZ NOT NULL,
    modified_by TEXT,
    status INT NOT NULL,
    candidate_users JSONB NOT NULL DEFAULT '[]',
    candidate_role TEXT,
    required_fields JSONB NOT NULL DEFAULT '[]'
);
CREATE INDEX IF NOT EXISTS ix_tasks_instance ON tasks(process_instance_id);
CREATE INDEX IF NOT EXISTS ix_tasks_type ON tasks(type);
CREATE INDEX IF NOT EXISTS ix_tasks_tenant ON tasks(tenant_id);
CREATE INDEX IF NOT EXISTS ix_tasks_assignee ON tasks(assignee);

CREATE TABLE history_event (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id) ON DELETE CASCADE,
    event_type TEXT NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    details TEXT,
    tenant_id TEXT,
    element_id TEXT NOT NULL,
    data TEXT
);
CREATE INDEX IF NOT EXISTS ix_history_event_instance ON history_event(process_instance_id);
CREATE INDEX IF NOT EXISTS ix_history_event_type ON history_event(event_type);
CREATE INDEX IF NOT EXISTS ix_history_event_element ON history_event(element_id);
CREATE INDEX IF NOT EXISTS ix_history_event_timestamp ON history_event(timestamp);
CREATE INDEX IF NOT EXISTS ix_history_event_tenant ON history_event(tenant_id);

CREATE TABLE incident (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id) ON DELETE CASCADE,
    type TEXT NOT NULL,
    message TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    tenant_id TEXT,
    state TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_incident_instance ON incident(process_instance_id);
CREATE INDEX IF NOT EXISTS ix_incident_type ON incident(type);
CREATE INDEX IF NOT EXISTS ix_incident_state ON incident(state);
CREATE INDEX IF NOT EXISTS ix_incident_created_at ON incident(created_at);
CREATE INDEX IF NOT EXISTS ix_incident_tenant ON incident(tenant_id);

CREATE TABLE multi_instance_execution (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id) ON DELETE CASCADE,
    activity_id TEXT NOT NULL,
    instance_count INT NOT NULL,
    completed_count INT NOT NULL,
    is_sequential BOOLEAN NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_mi_execution_instance ON multi_instance_execution(process_instance_id);
CREATE INDEX IF NOT EXISTS ix_mi_execution_activity ON multi_instance_execution(activity_id);
CREATE INDEX IF NOT EXISTS ix_mi_execution_instance_activity ON multi_instance_execution(process_instance_id, activity_id);

CREATE TABLE users (
    id TEXT PRIMARY KEY,
    username TEXT NOT NULL,
    email TEXT NOT NULL,
    is_active BOOLEAN NOT NULL,
    roles JSONB NOT NULL DEFAULT '[]',
    created_at TIMESTAMPTZ NOT NULL,
    last_modified TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_users_username ON users(username);
CREATE INDEX IF NOT EXISTS ix_users_email ON users(email);
CREATE INDEX IF NOT EXISTS ix_users_is_active ON users(is_active);
CREATE INDEX IF NOT EXISTS ix_users_created_at ON users(created_at);

-- Decision (DMN)
CREATE TABLE decision_definitions (
    id TEXT PRIMARY KEY,
    key TEXT NOT NULL,
    name TEXT NOT NULL,
    dmn_xml TEXT NOT NULL,
    tenant_id TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_decision_definition_key_tenant ON decision_definitions(key, tenant_id);
CREATE INDEX IF NOT EXISTS ix_decision_definition_key ON decision_definitions(key);

CREATE TABLE decision_instances (
    id TEXT PRIMARY KEY,
    decision_definition_key TEXT NOT NULL REFERENCES decision_definitions(key) ON DELETE CASCADE,
    evaluation_time TIMESTAMPTZ NOT NULL,
    tenant_id TEXT,
    error_message TEXT,
    input_variables JSONB NOT NULL DEFAULT '{}',
    output_variables JSONB NOT NULL DEFAULT '{}'
);
CREATE INDEX IF NOT EXISTS ix_decision_instance_key ON decision_instances(decision_definition_key);
CREATE INDEX IF NOT EXISTS ix_decision_instance_tenant ON decision_instances(tenant_id);
CREATE INDEX IF NOT EXISTS ix_decision_instance_time ON decision_instances(evaluation_time);
CREATE INDEX IF NOT EXISTS ix_decision_instance_key_tenant ON decision_instances(decision_definition_key, tenant_id);

CREATE TABLE dmn_decision_tables (
    key TEXT PRIMARY KEY,
    name TEXT,
    hit_policy TEXT,
    inputs JSONB NOT NULL DEFAULT '[]',
    outputs JSONB NOT NULL DEFAULT '[]',
    rules JSONB NOT NULL DEFAULT '[]'
);
CREATE INDEX IF NOT EXISTS ix_dmn_decision_table_name ON dmn_decision_tables(name);

-- Tenants
CREATE TABLE tenants (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_tenants_name ON tenants(name);

-- Simulation scenarios
CREATE TABLE simulation_scenarios (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    process_definition_id TEXT,
    bpmn_xml TEXT,
    max_steps INT NOT NULL,
    tenant_id TEXT
);
CREATE INDEX IF NOT EXISTS ix_sim_scenario_tenant ON simulation_scenarios(tenant_id);
CREATE INDEX IF NOT EXISTS ix_sim_scenario_name ON simulation_scenarios(name);

-- Process mining events
CREATE TABLE process_mining_events (
    id BIGSERIAL PRIMARY KEY,
    event_type TEXT NOT NULL,
    process_instance_id TEXT NOT NULL,
    task_id TEXT,
    activity_id TEXT,
    user_id TEXT,
    tenant_id TEXT,
    timestamp TIMESTAMPTZ NOT NULL,
    payload_json TEXT
);
CREATE INDEX IF NOT EXISTS ix_pme_event_type ON process_mining_events(event_type);
CREATE INDEX IF NOT EXISTS ix_pme_instance ON process_mining_events(process_instance_id);
CREATE INDEX IF NOT EXISTS ix_pme_tenant ON process_mining_events(tenant_id);
CREATE INDEX IF NOT EXISTS ix_pme_timestamp ON process_mining_events(timestamp);

-- Seed baseline data (idempotent)
INSERT INTO tenants(id,name,description,created_at) VALUES
 ('tenant-default','Default Tenant','Standard Mandant','2025-01-01T00:00:00Z'),
 ('tenant-acme','Acme Corp','Beispielkunde','2025-01-02T00:00:00Z')
ON CONFLICT DO NOTHING;

INSERT INTO engine_deployment(id,name,created_at,tenant_id) VALUES
 ('11111111-1111-1111-1111-111111111111','SampleDeployment','2025-01-01T00:00:00Z',NULL)
ON CONFLICT DO NOTHING;

INSERT INTO process_definition(id,key,name,version,bpmn_xml,created_at,deployment_id,tenant_id) VALUES
 ('22222222-2222-2222-2222-222222222222','SampleProcess','Sample Process',1,'<definitions id="SampleProcess"></definitions>','2025-01-01T00:00:00Z','11111111-1111-1111-1111-111111111111',NULL)
ON CONFLICT DO NOTHING;

INSERT INTO process_instance(id,process_definition_id,business_key,tenant_id,started_at,ended_at,state,instance_id,process_id,status,active_tasks,active_tokens,variables,created_at,last_modified) VALUES
 ('33333333-3333-3333-3333-333333333333','22222222-2222-2222-2222-222222222222','BK-001',NULL,'2025-01-01T00:00:00Z',NULL,'Running','sample-instance-1','SampleProcess',0,'[]','[]','{}','2025-01-01T00:00:00Z','2025-01-01T00:00:00Z')
ON CONFLICT DO NOTHING;

INSERT INTO job(id,process_instance_id,type,due_date,retries,error_message,tenant_id,state,payload) VALUES
 ('44444444-4444-4444-4444-444444444444','33333333-3333-3333-3333-333333333333','timer','2025-01-01T01:00:00Z',3,NULL,NULL,'Scheduled',NULL)
ON CONFLICT DO NOTHING;

INSERT INTO tasks(id,process_instance_id,name,type,assignee,tenant_id,created_at,completed_at,due_date,form_key,form_schema,last_modified,modified_by,status,candidate_users,candidate_role,required_fields) VALUES
 ('55555555-5555-5555-5555-555555555555','33333333-3333-3333-3333-333333333333','Review Request','userTask',NULL,NULL,'2025-01-01T00:00:00Z',NULL,'2025-01-03T00:00:00Z',NULL,NULL,'2025-01-01T00:00:00Z','',0,'[]','', '[]')
ON CONFLICT DO NOTHING;

INSERT INTO variable(id,scope_id,name,type,value,tenant_id,process_instance_id,created_at) VALUES
 ('66666666-6666-6666-6666-666666666666','33333333-3333-3333-3333-333333333333','approvalRequired','boolean','true',NULL,'33333333-3333-3333-3333-333333333333','2025-01-01T00:00:00Z')
ON CONFLICT DO NOTHING;

INSERT INTO history_event(id,process_instance_id,event_type,timestamp,details,tenant_id,element_id,data) VALUES
 ('77777777-7777-7777-7777-777777777777','33333333-3333-3333-3333-333333333333','PROCESS_STARTED','2025-01-01T00:00:00Z','Process instance started.',NULL,'startEvent1',NULL)
ON CONFLICT DO NOTHING;

INSERT INTO incident(id,process_instance_id,type,message,created_at,tenant_id,state) VALUES
 ('88888888-8888-8888-8888-888888888888','33333333-3333-3333-3333-333333333333','None','No incident','2025-01-01T00:00:00Z',NULL,'Resolved')
ON CONFLICT DO NOTHING;

INSERT INTO multi_instance_execution(id,process_instance_id,activity_id,instance_count,completed_count,is_sequential) VALUES
 ('99999999-9999-9999-9999-999999999999','33333333-3333-3333-3333-333333333333','activity_multi_1',3,0,TRUE)
ON CONFLICT DO NOTHING;

INSERT INTO execution_token(id,process_instance_id,current_node_id,node_type,variables,created_at,assigned_worker,assigned_at,retry_count,state) VALUES
 ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','33333333-3333-3333-3333-333333333333','startEvent1','startEvent','{}','2025-01-01T00:00:00Z',NULL,NULL,0,'Active')
ON CONFLICT DO NOTHING;

INSERT INTO users(id,username,email,is_active,roles,created_at,last_modified) VALUES
 ('1','admin','admin@example.com',TRUE,'["admin"]','2025-01-01T00:00:00Z','2025-01-01T00:00:00Z'),
 ('2','user1','user1@example.com',TRUE,'["user"]','2025-01-01T00:00:00Z','2025-01-01T00:00:00Z'),
 ('3','user2','user2@example.com',TRUE,'["user"]','2025-01-01T00:00:00Z','2025-01-01T00:00:00Z')
ON CONFLICT DO NOTHING;

INSERT INTO process_mining_events(id,event_type,process_instance_id,task_id,activity_id,user_id,tenant_id,timestamp,payload_json) VALUES
 (1,'PROCESS_STARTED','33333333-3333-3333-3333-333333333333',NULL,'startEvent1','system','tenant-default','2025-01-01T00:00:00Z',NULL),
 (2,'TASK_CREATED','33333333-3333-3333-3333-333333333333','55555555-5555-5555-5555-555555555555','activity_userTask_1',NULL,'tenant-default','2025-01-01T00:01:00Z','{"name":"Review Request"}')
ON CONFLICT DO NOTHING;

INSERT INTO simulation_scenarios(id,name,description,process_definition_id,bpmn_xml,max_steps,tenant_id) VALUES
 ('sim-sample-1','Throughput Test','Ein einfacher Simulationstest','22222222-2222-2222-2222-222222222222',NULL,100,'tenant-default')
ON CONFLICT DO NOTHING;

-- Downgrade (manual rollback)
-- DROP TABLE IF EXISTS process_mining_events, simulation_scenarios, decision_instances,
--   dmn_decision_tables, decision_definitions, users, multi_instance_execution, incident,
--   history_event, tasks, job, variable, execution_token, process_instance, process_definition,
--   engine_deployment, tenants CASCADE;
