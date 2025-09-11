-- Beispiel-Datenbankschema für VertexBPMN (PostgreSQL)

CREATE TABLE engine_deployment (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    deployed_at TIMESTAMP NOT NULL,
    tenant_id UUID
);

CREATE TABLE process_definition (
    id UUID PRIMARY KEY,
    key VARCHAR(255) NOT NULL,
    version INT NOT NULL,
    deployment_id UUID REFERENCES engine_deployment(id),
    bpmn_xml TEXT,
    resource_name VARCHAR(255)
);

CREATE TABLE process_instance (
    id UUID PRIMARY KEY,
    process_definition_id UUID REFERENCES process_definition(id),
    business_key VARCHAR(255),
    state VARCHAR(50),
    start_time TIMESTAMP,
    end_time TIMESTAMP,
    root_scope_id UUID
);

CREATE TABLE execution_token (
    id UUID PRIMARY KEY,
    process_instance_id UUID REFERENCES process_instance(id),
    activity_id VARCHAR(255),
    state VARCHAR(50),
    priority INT,
    created_at TIMESTAMP,
    updated_at TIMESTAMP
);

CREATE TABLE variable (
    id UUID PRIMARY KEY,
    execution_id UUID REFERENCES execution_token(id),
    name VARCHAR(255),
    type VARCHAR(50),
    value_json JSONB,
    created_at TIMESTAMP
);

CREATE TABLE job (
    id UUID PRIMARY KEY,
    process_instance_id UUID REFERENCES process_instance(id),
    handler_type VARCHAR(255),
    handler_configuration JSONB,
    retries INT,
    due_date TIMESTAMP,
    lock_owner VARCHAR(255),
    lock_expiration TIMESTAMP
);

CREATE TABLE task (
    id UUID PRIMARY KEY,
    execution_id UUID REFERENCES execution_token(id),
    assignee VARCHAR(255),
    candidate_groups JSONB,
    create_time TIMESTAMP,
    complete_time TIMESTAMP,
    form_key VARCHAR(255)
);

CREATE TABLE history_event (
    id UUID PRIMARY KEY,
    process_instance_id UUID REFERENCES process_instance(id),
    activity_id VARCHAR(255),
    event_type VARCHAR(50),
    timestamp TIMESTAMP,
    payload JSONB
);

CREATE TABLE incident (
    id UUID PRIMARY KEY,
    job_id UUID REFERENCES job(id),
    incident_type VARCHAR(50),
    message TEXT,
    created_at TIMESTAMP
);

-- Index-Beispiele
CREATE INDEX idx_process_definition_key_version ON process_definition(key, version);
CREATE INDEX idx_job_due_date ON job(due_date);
CREATE INDEX idx_job_lock_owner ON job(lock_owner);
