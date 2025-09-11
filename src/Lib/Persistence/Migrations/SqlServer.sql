-- VertexBPMN DDL für SQL Server
CREATE TABLE engine_deployment (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    name NVARCHAR(255) NOT NULL,
    created_at DATETIME2 NOT NULL,
    tenant_id NVARCHAR(255)
);

CREATE TABLE process_definition (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    key NVARCHAR(255) NOT NULL,
    name NVARCHAR(255) NOT NULL,
    bpmn_xml NVARCHAR(MAX) NOT NULL,
    deployment_id UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES engine_deployment(id),
    tenant_id NVARCHAR(255)
);

CREATE TABLE process_instance (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    process_definition_id UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES process_definition(id),
    business_key NVARCHAR(255),
    state NVARCHAR(50) NOT NULL,
    started_at DATETIME2 NOT NULL,
    ended_at DATETIME2,
    tenant_id NVARCHAR(255)
);

CREATE TABLE execution_token (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    process_instance_id UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES process_instance(id),
    element_id NVARCHAR(255) NOT NULL,
    state NVARCHAR(50) NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE variable (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    process_instance_id UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES process_instance(id),
    name NVARCHAR(255) NOT NULL,
    type NVARCHAR(50) NOT NULL,
    value NVARCHAR(MAX) NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE job (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    process_instance_id UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES process_instance(id),
    type NVARCHAR(50) NOT NULL,
    state NVARCHAR(50) NOT NULL,
    retries INT NOT NULL,
    due_date DATETIME2 NOT NULL,
    payload NVARCHAR(MAX)
);

CREATE TABLE task (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    process_instance_id UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES process_instance(id),
    name NVARCHAR(255) NOT NULL,
    assignee NVARCHAR(255) NOT NULL,
    state NVARCHAR(50) NOT NULL,
    created_at DATETIME2 NOT NULL,
    completed_at DATETIME2
);

CREATE TABLE history_event (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    process_instance_id UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES process_instance(id),
    event_type NVARCHAR(50) NOT NULL,
    element_id NVARCHAR(255) NOT NULL,
    data NVARCHAR(MAX),
    timestamp DATETIME2 NOT NULL
);

CREATE TABLE incident (
    id UNIQUEIDENTIFIER PRIMARY KEY,
    process_instance_id UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES process_instance(id),
    type NVARCHAR(50) NOT NULL,
    message NVARCHAR(MAX) NOT NULL,
    created_at DATETIME2 NOT NULL
);
