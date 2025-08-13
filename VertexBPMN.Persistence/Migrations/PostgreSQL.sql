-- VertexBPMN DDL für PostgreSQL
CREATE TABLE engine_deployment (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    tenant_id TEXT
);

CREATE TABLE process_definition (
    id UUID PRIMARY KEY,
    key TEXT NOT NULL,
    name TEXT NOT NULL,
    bpmn_xml TEXT NOT NULL,
    deployment_id UUID NOT NULL REFERENCES engine_deployment(id),
    tenant_id TEXT
);

CREATE TABLE process_instance (
    id UUID PRIMARY KEY,
    process_definition_id UUID NOT NULL REFERENCES process_definition(id),
    business_key TEXT,
    state TEXT NOT NULL,
    started_at TIMESTAMPTZ NOT NULL,
    ended_at TIMESTAMPTZ,
    tenant_id TEXT
);

CREATE TABLE execution_token (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id),
    element_id TEXT NOT NULL,
    state TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE variable (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id),
    name TEXT NOT NULL,
    type TEXT NOT NULL,
    value TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE job (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id),
    type TEXT NOT NULL,
    state TEXT NOT NULL,
    retries INT NOT NULL,
    due_date TIMESTAMPTZ NOT NULL,
    payload TEXT
);

CREATE TABLE task (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id),
    name TEXT NOT NULL,
    assignee TEXT NOT NULL,
    state TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ
);

CREATE TABLE history_event (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id),
    event_type TEXT NOT NULL,
    element_id TEXT NOT NULL,
    data TEXT,
    timestamp TIMESTAMPTZ NOT NULL
);

CREATE TABLE incident (
    id UUID PRIMARY KEY,
    process_instance_id UUID NOT NULL REFERENCES process_instance(id),
    type TEXT NOT NULL,
    message TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
);
