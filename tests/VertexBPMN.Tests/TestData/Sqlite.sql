-- VertexBPMN Consolidated DDL for SQLite
-- Warning: SQLite lacks some advanced features (JSONB). JSON stored as TEXT.
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS EngineDeployment (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    TenantId TEXT
);
CREATE INDEX IF NOT EXISTS IX_EngineDeployment_CreatedAt ON EngineDeployment(CreatedAt);
CREATE INDEX IF NOT EXISTS IX_EngineDeployment_Tenant ON EngineDeployment(TenantId);
CREATE INDEX IF NOT EXISTS IX_EngineDeployment_Name ON EngineDeployment(Name);

CREATE TABLE IF NOT EXISTS ProcessDefinition (
    Id TEXT PRIMARY KEY,
    Key TEXT NOT NULL,
    Name TEXT NOT NULL,
    Version INTEGER NOT NULL,
    BpmnXml TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    DeploymentId TEXT NOT NULL REFERENCES EngineDeployment(Id) ON DELETE CASCADE,
    TenantId TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_ProcessDefinition_Key_Version ON ProcessDefinition(Key, Version);
CREATE INDEX IF NOT EXISTS IX_ProcessDefinition_Key ON ProcessDefinition(Key);
CREATE INDEX IF NOT EXISTS IX_ProcessDefinition_Tenant ON ProcessDefinition(TenantId);
CREATE INDEX IF NOT EXISTS IX_ProcessDefinition_Deployment ON ProcessDefinition(DeploymentId);

CREATE TABLE IF NOT EXISTS ProcessInstance (
    Id TEXT PRIMARY KEY,
    ProcessDefinitionId TEXT NOT NULL REFERENCES ProcessDefinition(Id) ON DELETE RESTRICT,
    BusinessKey TEXT,
    TenantId TEXT,
    StartedAt TEXT NOT NULL,
    EndedAt TEXT,
    State TEXT NOT NULL,
    InstanceId TEXT NOT NULL,
    ProcessId TEXT NOT NULL,
    Status INTEGER NOT NULL,
    ActiveTasks TEXT NOT NULL DEFAULT '[]',
    ActiveTokens TEXT NOT NULL DEFAULT '[]',
    Variables TEXT NOT NULL DEFAULT '{}',
    CreatedAt TEXT NOT NULL,
    LastModified TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_ProcessInstance_Definition ON ProcessInstance(ProcessDefinitionId);
CREATE INDEX IF NOT EXISTS IX_ProcessInstance_BusinessKey ON ProcessInstance(BusinessKey);
CREATE INDEX IF NOT EXISTS IX_ProcessInstance_Tenant ON ProcessInstance(TenantId);
CREATE INDEX IF NOT EXISTS IX_ProcessInstance_State ON ProcessInstance(State);
CREATE INDEX IF NOT EXISTS IX_ProcessInstance_StartedAt ON ProcessInstance(StartedAt);
CREATE VIEW IF NOT EXISTS ProcessInstances AS SELECT * FROM ProcessInstance;

CREATE TABLE IF NOT EXISTS ExecutionToken (
    Id TEXT PRIMARY KEY,
    ProcessInstanceId TEXT NOT NULL REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    CurrentNodeId TEXT NOT NULL,
    NodeType TEXT NOT NULL,
    Variables TEXT NOT NULL DEFAULT '{}',
    CreatedAt TEXT NOT NULL,
    AssignedWorker TEXT,
    AssignedAt TEXT,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    State TEXT
);
CREATE INDEX IF NOT EXISTS IX_ExecutionToken_Instance ON ExecutionToken(ProcessInstanceId);
CREATE INDEX IF NOT EXISTS IX_ExecutionToken_CurrentNode ON ExecutionToken(CurrentNodeId);
CREATE INDEX IF NOT EXISTS IX_ExecutionToken_State ON ExecutionToken(State);
CREATE INDEX IF NOT EXISTS IX_ExecutionToken_AssignedWorker ON ExecutionToken(AssignedWorker);
CREATE INDEX IF NOT EXISTS IX_ExecutionToken_CreatedAt ON ExecutionToken(CreatedAt);

CREATE TABLE IF NOT EXISTS Variable (
    Id TEXT PRIMARY KEY,
    ScopeId TEXT NOT NULL,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,
    Value TEXT,
    TenantId TEXT,
    ProcessInstanceId TEXT NOT NULL REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Variable_Scope ON Variable(ScopeId);
CREATE INDEX IF NOT EXISTS IX_Variable_Name ON Variable(Name);
CREATE INDEX IF NOT EXISTS IX_Variable_Type ON Variable(Type);
CREATE INDEX IF NOT EXISTS IX_Variable_Tenant ON Variable(TenantId);
CREATE INDEX IF NOT EXISTS IX_Variable_Instance ON Variable(ProcessInstanceId);

CREATE TABLE IF NOT EXISTS Job (
    Id TEXT PRIMARY KEY,
    ProcessInstanceId TEXT NOT NULL REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    Type TEXT NOT NULL,
    DueDate TEXT NOT NULL,
    Retries INTEGER NOT NULL,
    ErrorMessage TEXT,
    TenantId TEXT,
    State TEXT NOT NULL,
    Payload TEXT
);
CREATE INDEX IF NOT EXISTS IX_Job_Instance ON Job(ProcessInstanceId);
CREATE INDEX IF NOT EXISTS IX_Job_Type ON Job(Type);
CREATE INDEX IF NOT EXISTS IX_Job_State ON Job(State);
CREATE INDEX IF NOT EXISTS IX_Job_DueDate ON Job(DueDate);
CREATE INDEX IF NOT EXISTS IX_Job_Tenant ON Job(TenantId);

CREATE TABLE IF NOT EXISTS Tasks (
    Id TEXT PRIMARY KEY,
    ProcessInstanceId TEXT NOT NULL REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,
    Assignee TEXT,
    TenantId TEXT,
    CreatedAt TEXT NOT NULL,
    CompletedAt TEXT,
    DueDate TEXT,
    FormKey TEXT,
    FormSchema TEXT,
    LastModified TEXT NOT NULL,
    ModifiedBy TEXT,
    Status INTEGER NOT NULL,
    CandidateUsers TEXT NOT NULL DEFAULT '[]',
    CandidateRole TEXT,
    RequiredFields TEXT NOT NULL DEFAULT '[]'
);
CREATE INDEX IF NOT EXISTS IX_Tasks_Instance ON Tasks(ProcessInstanceId);
CREATE INDEX IF NOT EXISTS IX_Tasks_Type ON Tasks(Type);
CREATE INDEX IF NOT EXISTS IX_Tasks_Tenant ON Tasks(TenantId);
CREATE INDEX IF NOT EXISTS IX_Tasks_Assignee ON Tasks(Assignee);

CREATE TABLE IF NOT EXISTS HistoryEvent (
    Id TEXT PRIMARY KEY,
    ProcessInstanceId TEXT NOT NULL REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    EventType TEXT NOT NULL,
    Timestamp TEXT NOT NULL,
    Details TEXT,
    TenantId TEXT,
    ElementId TEXT NOT NULL,
    Data TEXT
);
CREATE INDEX IF NOT EXISTS IX_HistoryEvent_Instance ON HistoryEvent(ProcessInstanceId);
CREATE INDEX IF NOT EXISTS IX_HistoryEvent_Type ON HistoryEvent(EventType);
CREATE INDEX IF NOT EXISTS IX_HistoryEvent_Element ON HistoryEvent(ElementId);
CREATE INDEX IF NOT EXISTS IX_HistoryEvent_Timestamp ON HistoryEvent(Timestamp);
CREATE INDEX IF NOT EXISTS IX_HistoryEvent_Tenant ON HistoryEvent(TenantId);

CREATE TABLE IF NOT EXISTS Incident (
    Id TEXT PRIMARY KEY,
    ProcessInstanceId TEXT NOT NULL REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    Type TEXT NOT NULL,
    Message TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    TenantId TEXT,
    State TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Incident_Instance ON Incident(ProcessInstanceId);
CREATE INDEX IF NOT EXISTS IX_Incident_Type ON Incident(Type);
CREATE INDEX IF NOT EXISTS IX_Incident_State ON Incident(State);
CREATE INDEX IF NOT EXISTS IX_Incident_CreatedAt ON Incident(CreatedAt);
CREATE INDEX IF NOT EXISTS IX_Incident_Tenant ON Incident(TenantId);

CREATE TABLE IF NOT EXISTS MultiInstanceExecution (
    Id TEXT PRIMARY KEY,
    ProcessInstanceId TEXT NOT NULL REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    ActivityId TEXT NOT NULL,
    InstanceCount INTEGER NOT NULL,
    CompletedCount INTEGER NOT NULL,
    IsSequential INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_MIExec_Instance ON MultiInstanceExecution(ProcessInstanceId);
CREATE INDEX IF NOT EXISTS IX_MIExec_Activity ON MultiInstanceExecution(ActivityId);
CREATE INDEX IF NOT EXISTS IX_MIExec_Instance_Activity ON MultiInstanceExecution(ProcessInstanceId, ActivityId);

CREATE TABLE IF NOT EXISTS Users (
    Id TEXT PRIMARY KEY,
    Username TEXT NOT NULL,
    Email TEXT NOT NULL,
    IsActive INTEGER NOT NULL,
    Roles TEXT NOT NULL DEFAULT '[]',
    CreatedAt TEXT NOT NULL,
    LastModified TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);
CREATE INDEX IF NOT EXISTS IX_Users_Email ON Users(Email);
CREATE INDEX IF NOT EXISTS IX_Users_IsActive ON Users(IsActive);
CREATE INDEX IF NOT EXISTS IX_Users_CreatedAt ON Users(CreatedAt);

CREATE TABLE IF NOT EXISTS DecisionDefinitions (
    Id TEXT PRIMARY KEY,
    [Key] TEXT NOT NULL,
    Name TEXT NOT NULL,
    DmnXml TEXT NOT NULL,
    TenantId TEXT,
    CreatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_DecisionDefinition_Key_Tenant ON DecisionDefinitions([Key], TenantId);
CREATE INDEX IF NOT EXISTS IX_DecisionDefinition_Key ON DecisionDefinitions([Key]);

CREATE TABLE IF NOT EXISTS DecisionInstances (
    Id TEXT PRIMARY KEY,
    DecisionDefinitionKey TEXT NOT NULL REFERENCES DecisionDefinitions([Key]) ON DELETE CASCADE,
    EvaluationTime TEXT NOT NULL,
    TenantId TEXT,
    ErrorMessage TEXT,
    InputVariables TEXT NOT NULL DEFAULT '{}',
    OutputVariables TEXT NOT NULL DEFAULT '{}'
);
CREATE INDEX IF NOT EXISTS IX_DecisionInstance_Key ON DecisionInstances(DecisionDefinitionKey);
CREATE INDEX IF NOT EXISTS IX_DecisionInstance_Tenant ON DecisionInstances(TenantId);
CREATE INDEX IF NOT EXISTS IX_DecisionInstance_Time ON DecisionInstances(EvaluationTime);
CREATE INDEX IF NOT EXISTS IX_DecisionInstance_Key_Tenant ON DecisionInstances(DecisionDefinitionKey, TenantId);

CREATE TABLE IF NOT EXISTS DmnDecisionTables (
    [Key] TEXT PRIMARY KEY,
    Name TEXT,
    HitPolicy TEXT,
    Inputs TEXT NOT NULL DEFAULT '[]',
    Outputs TEXT NOT NULL DEFAULT '[]',
    Rules TEXT NOT NULL DEFAULT '[]'
);
CREATE INDEX IF NOT EXISTS IX_DmnDecisionTables_Name ON DmnDecisionTables(Name);

CREATE TABLE IF NOT EXISTS Tenants (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT,
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Tenants_Name ON Tenants(Name);

CREATE TABLE IF NOT EXISTS SimulationScenarios (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT,
    ProcessDefinitionId TEXT,
    BpmnXml TEXT,
    MaxSteps INTEGER NOT NULL,
    TenantId TEXT
);
CREATE INDEX IF NOT EXISTS IX_SimScenarios_Tenant ON SimulationScenarios(TenantId);
CREATE INDEX IF NOT EXISTS IX_SimScenarios_Name ON SimulationScenarios(Name);

CREATE TABLE IF NOT EXISTS ProcessMiningEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventType TEXT NOT NULL,
    ProcessInstanceId TEXT NOT NULL,
    TaskId TEXT,
    ActivityId TEXT,
    UserId TEXT,
    TenantId TEXT,
    Timestamp TEXT NOT NULL,
    PayloadJson TEXT
);
CREATE INDEX IF NOT EXISTS IX_PME_EventType ON ProcessMiningEvents(EventType);
CREATE INDEX IF NOT EXISTS IX_PME_Instance ON ProcessMiningEvents(ProcessInstanceId);
CREATE INDEX IF NOT EXISTS IX_PME_Tenant ON ProcessMiningEvents(TenantId);
CREATE INDEX IF NOT EXISTS IX_PME_Timestamp ON ProcessMiningEvents(Timestamp);

-- Idempotent seed (INSERT OR IGNORE) -----------------------------------------
INSERT OR IGNORE INTO Tenants(Id,Name,Description,CreatedAt) VALUES
 ('tenant-default','Default Tenant','Standard Mandant','2025-01-01T00:00:00Z'),
 ('tenant-acme','Acme Corp','Beispielkunde','2025-01-02T00:00:00Z');

INSERT OR IGNORE INTO EngineDeployment(Id,Name,CreatedAt,TenantId) VALUES
 ('11111111-1111-1111-1111-111111111111','SampleDeployment','2025-01-01T00:00:00Z',NULL);

INSERT OR IGNORE INTO ProcessDefinition(Id,Key,Name,Version,BpmnXml,CreatedAt,DeploymentId,TenantId) VALUES
 ('22222222-2222-2222-2222-222222222222','SampleProcess','Sample Process',1,'<definitions id="SampleProcess"></definitions>','2025-01-01T00:00:00Z','11111111-1111-1111-1111-111111111111',NULL);

INSERT OR IGNORE INTO ProcessInstance(Id,ProcessDefinitionId,BusinessKey,TenantId,StartedAt,EndedAt,State,InstanceId,ProcessId,Status,ActiveTasks,ActiveTokens,Variables,CreatedAt,LastModified) VALUES
 ('33333333-3333-3333-3333-333333333333','22222222-2222-2222-2222-222222222222','BK-001',NULL,'2025-01-01T00:00:00Z',NULL,'Running','sample-instance-1','SampleProcess',0,'[]','[]','{}','2025-01-01T00:00:00Z','2025-01-01T00:00:00Z');

INSERT OR IGNORE INTO Job(Id,ProcessInstanceId,Type,DueDate,Retries,ErrorMessage,TenantId,State,Payload) VALUES
 ('44444444-4444-4444-4444-444444444444','33333333-3333-3333-3333-333333333333','timer','2025-01-01T01:00:00Z',3,NULL,NULL,'Scheduled',NULL);

INSERT OR IGNORE INTO Tasks(Id,ProcessInstanceId,Name,Type,Assignee,TenantId,CreatedAt,CompletedAt,DueDate,FormKey,FormSchema,LastModified,ModifiedBy,Status,CandidateUsers,CandidateRole,RequiredFields) VALUES
 ('55555555-5555-5555-5555-555555555555','33333333-3333-3333-3333-333333333333','Review Request','userTask',NULL,NULL,'2025-01-01T00:00:00Z',NULL,'2025-01-03T00:00:00Z',NULL,NULL,'2025-01-01T00:00:00Z','',0,'[]',NULL,'[]');

INSERT OR IGNORE INTO Variable(Id,ScopeId,Name,Type,Value,TenantId,ProcessInstanceId,CreatedAt) VALUES
 ('66666666-6666-6666-6666-666666666666','33333333-3333-3333-3333-333333333333','approvalRequired','boolean','true',NULL,'33333333-3333-3333-3333-333333333333','2025-01-01T00:00:00Z');

INSERT OR IGNORE INTO HistoryEvent(Id,ProcessInstanceId,EventType,Timestamp,Details,TenantId,ElementId,Data) VALUES
 ('77777777-7777-7777-7777-777777777777','33333333-3333-3333-3333-333333333333','PROCESS_STARTED','2025-01-01T00:00:00Z','Process instance started.',NULL,'startEvent1',NULL);

INSERT OR IGNORE INTO Incident(Id,ProcessInstanceId,Type,Message,CreatedAt,TenantId,State) VALUES
 ('88888888-8888-8888-8888-888888888888','33333333-3333-3333-3333-333333333333','None','No incident','2025-01-01T00:00:00Z',NULL,'Resolved');

INSERT OR IGNORE INTO MultiInstanceExecution(Id,ProcessInstanceId,ActivityId,InstanceCount,CompletedCount,IsSequential) VALUES
 ('99999999-9999-9999-9999-999999999999','33333333-3333-3333-3333-333333333333','activity_multi_1',3,0,1);

INSERT OR IGNORE INTO ExecutionToken(Id,ProcessInstanceId,CurrentNodeId,NodeType,Variables,CreatedAt,AssignedWorker,AssignedAt,RetryCount,State) VALUES
 ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','33333333-3333-3333-3333-333333333333','startEvent1','startEvent','{}','2025-01-01T00:00:00Z',NULL,NULL,0,'Active');

INSERT OR IGNORE INTO Users(Id,Username,Email,IsActive,Roles,CreatedAt,LastModified) VALUES
 ('1','admin','admin@example.com',1,'["admin"]','2025-01-01T00:00:00Z','2025-01-01T00:00:00Z'),
 ('2','user1','user1@example.com',1,'["user"]','2025-01-01T00:00:00Z','2025-01-01T00:00:00Z'),
 ('3','user2','user2@example.com',1,'["user"]','2025-01-01T00:00:00Z','2025-01-01T00:00:00Z');

INSERT OR IGNORE INTO ProcessMiningEvents(Id,EventType,ProcessInstanceId,TaskId,ActivityId,UserId,TenantId,Timestamp,PayloadJson) VALUES
 (1,'PROCESS_STARTED','33333333-3333-3333-3333-333333333333',NULL,'startEvent1','system','tenant-default','2025-01-01T00:00:00Z',NULL),
 (2,'TASK_CREATED','33333333-3333-3333-3333-333333333333','55555555-5555-5555-5555-555555555555','activity_userTask_1',NULL,'tenant-default','2025-01-01T00:01:00Z','{"name":"Review Request"}');

INSERT OR IGNORE INTO SimulationScenarios(Id,Name,Description,ProcessDefinitionId,BpmnXml,MaxSteps,TenantId) VALUES
 ('sim-sample-1','Throughput Test','Ein einfacher Simulationstest','22222222-2222-2222-2222-222222222222',NULL,100,'tenant-default');

DROP TABLE IF EXISTS WorkflowTriggers;

-- Downgrade (drop all) ------------------------------------------------------
-- To rollback execute:
-- DROP TABLE ProcessMiningEvents; DROP TABLE SimulationScenarios; DROP TABLE DecisionInstances; DROP TABLE DmnDecisionTables; DROP TABLE DecisionDefinitions; DROP TABLE Users; DROP TABLE MultiInstanceExecution; DROP TABLE Incident; DROP TABLE HistoryEvent; DROP TABLE Tasks; DROP TABLE Job; DROP TABLE Variable; DROP TABLE ExecutionToken; DROP TABLE ProcessInstance; DROP TABLE ProcessDefinition; DROP TABLE EngineDeployment; DROP TABLE Tenants;
