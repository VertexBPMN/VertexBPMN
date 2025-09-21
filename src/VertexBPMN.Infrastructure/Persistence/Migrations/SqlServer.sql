CREATE TABLE EngineDeployment (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(500) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    TenantId NVARCHAR(64) NULL
);
CREATE INDEX IX_EngineDeployment_CreatedAt ON EngineDeployment(CreatedAt);
CREATE INDEX IX_EngineDeployment_Tenant ON EngineDeployment(TenantId);
CREATE INDEX IX_EngineDeployment_Name ON EngineDeployment(Name);

CREATE TABLE ProcessDefinition (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    [Key] NVARCHAR(255) NOT NULL,
    Name NVARCHAR(500) NOT NULL,
    Version INT NOT NULL,
    BpmnXml NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    DeploymentId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES EngineDeployment(Id) ON DELETE CASCADE,
    TenantId NVARCHAR(64) NULL
);
CREATE UNIQUE INDEX UX_ProcessDefinition_Key_Version ON ProcessDefinition([Key], Version);
CREATE INDEX IX_ProcessDefinition_Key ON ProcessDefinition([Key]);
CREATE INDEX IX_ProcessDefinition_Tenant ON ProcessDefinition(TenantId);
CREATE INDEX IX_ProcessDefinition_Deployment ON ProcessDefinition(DeploymentId);

CREATE TABLE ProcessInstance (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProcessDefinitionId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES ProcessDefinition(Id) ON DELETE NO ACTION,
    BusinessKey NVARCHAR(255) NULL,
    TenantId NVARCHAR(64) NULL,
    StartedAt DATETIME2 NOT NULL,
    EndedAt DATETIME2 NULL,
    State NVARCHAR(50) NOT NULL,
    InstanceId NVARCHAR(255) NOT NULL,
    ProcessId NVARCHAR(255) NOT NULL,
    Status INT NOT NULL,
    ActiveTasks NVARCHAR(MAX) NOT NULL,
    ActiveTokens NVARCHAR(MAX) NOT NULL,
    Variables NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    LastModified DATETIME2 NOT NULL
);
CREATE INDEX IX_ProcessInstance_Definition ON ProcessInstance(ProcessDefinitionId);
CREATE INDEX IX_ProcessInstance_BusinessKey ON ProcessInstance(BusinessKey);
CREATE INDEX IX_ProcessInstance_Tenant ON ProcessInstance(TenantId);
CREATE INDEX IX_ProcessInstance_State ON ProcessInstance(State);
CREATE INDEX IX_ProcessInstance_StartedAt ON ProcessInstance(StartedAt);

CREATE TABLE ExecutionToken (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProcessInstanceId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    CurrentNodeId NVARCHAR(255) NOT NULL,
    NodeType NVARCHAR(100) NOT NULL,
    Variables NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    AssignedWorker NVARCHAR(255) NULL,
    AssignedAt DATETIME2 NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    State NVARCHAR(50) NULL
);
CREATE INDEX IX_ExecutionToken_Instance ON ExecutionToken(ProcessInstanceId);
CREATE INDEX IX_ExecutionToken_CurrentNode ON ExecutionToken(CurrentNodeId);
CREATE INDEX IX_ExecutionToken_State ON ExecutionToken(State);
CREATE INDEX IX_ExecutionToken_AssignedWorker ON ExecutionToken(AssignedWorker);
CREATE INDEX IX_ExecutionToken_CreatedAt ON ExecutionToken(CreatedAt);

CREATE TABLE Variable (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ScopeId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(255) NOT NULL,
    Type NVARCHAR(100) NOT NULL,
    Value NVARCHAR(MAX) NULL,
    TenantId NVARCHAR(64) NULL,
    ProcessInstanceId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    CreatedAt DATETIME2 NOT NULL
);
CREATE INDEX IX_Variable_Scope ON Variable(ScopeId);
CREATE INDEX IX_Variable_Name ON Variable(Name);
CREATE INDEX IX_Variable_Type ON Variable(Type);
CREATE INDEX IX_Variable_Tenant ON Variable(TenantId);
CREATE INDEX IX_Variable_Instance ON Variable(ProcessInstanceId);

CREATE TABLE Job (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProcessInstanceId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    Type NVARCHAR(100) NOT NULL,
    DueDate DATETIME2 NOT NULL,
    Retries INT NOT NULL,
    ErrorMessage NVARCHAR(4000) NULL,
    TenantId NVARCHAR(64) NULL,
    State NVARCHAR(50) NOT NULL,
    Payload NVARCHAR(MAX) NULL
);
CREATE INDEX IX_Job_Instance ON Job(ProcessInstanceId);
CREATE INDEX IX_Job_Type ON Job(Type);
CREATE INDEX IX_Job_State ON Job(State);
CREATE INDEX IX_Job_DueDate ON Job(DueDate);
CREATE INDEX IX_Job_Tenant ON Job(TenantId);

CREATE TABLE Tasks (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProcessInstanceId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    Name NVARCHAR(500) NOT NULL,
    Type NVARCHAR(100) NOT NULL,
    Assignee NVARCHAR(255) NULL,
    TenantId NVARCHAR(64) NULL,
    CreatedAt DATETIME2 NOT NULL,
    CompletedAt DATETIME2 NULL,
    DueDate DATETIME2 NULL,
    FormKey NVARCHAR(255) NULL,
    FormSchema NVARCHAR(MAX) NULL,
    LastModified DATETIME2 NOT NULL,
    ModifiedBy NVARCHAR(255) NULL,
    Status INT NOT NULL,
    CandidateUsers NVARCHAR(MAX) NOT NULL,
    CandidateRole NVARCHAR(255) NULL,
    RequiredFields NVARCHAR(MAX) NOT NULL
);
CREATE INDEX IX_Tasks_Instance ON Tasks(ProcessInstanceId);
CREATE INDEX IX_Tasks_Type ON Tasks(Type);
CREATE INDEX IX_Tasks_Tenant ON Tasks(TenantId);
CREATE INDEX IX_Tasks_Assignee ON Tasks(Assignee);

CREATE TABLE HistoryEvent (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProcessInstanceId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    EventType NVARCHAR(100) NOT NULL,
    Timestamp DATETIME2 NOT NULL,
    Details NVARCHAR(4000) NULL,
    TenantId NVARCHAR(64) NULL,
    ElementId NVARCHAR(255) NOT NULL,
    Data NVARCHAR(4000) NULL
);
CREATE INDEX IX_HistoryEvent_Instance ON HistoryEvent(ProcessInstanceId);
CREATE INDEX IX_HistoryEvent_Type ON HistoryEvent(EventType);
CREATE INDEX IX_HistoryEvent_Element ON HistoryEvent(ElementId);
CREATE INDEX IX_HistoryEvent_Timestamp ON HistoryEvent(Timestamp);
CREATE INDEX IX_HistoryEvent_Tenant ON HistoryEvent(TenantId);

CREATE TABLE Incident (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProcessInstanceId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    Type NVARCHAR(100) NOT NULL,
    Message NVARCHAR(4000) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    TenantId NVARCHAR(64) NULL,
    State NVARCHAR(50) NOT NULL
);
CREATE INDEX IX_Incident_Instance ON Incident(ProcessInstanceId);
CREATE INDEX IX_Incident_Type ON Incident(Type);
CREATE INDEX IX_Incident_State ON Incident(State);
CREATE INDEX IX_Incident_CreatedAt ON Incident(CreatedAt);
CREATE INDEX IX_Incident_Tenant ON Incident(TenantId);

CREATE TABLE MultiInstanceExecution (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProcessInstanceId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES ProcessInstance(Id) ON DELETE CASCADE,
    ActivityId NVARCHAR(255) NOT NULL,
    InstanceCount INT NOT NULL,
    CompletedCount INT NOT NULL,
    IsSequential BIT NOT NULL
);
CREATE INDEX IX_MIExec_Instance ON MultiInstanceExecution(ProcessInstanceId);
CREATE INDEX IX_MIExec_Activity ON MultiInstanceExecution(ActivityId);
CREATE INDEX IX_MIExec_Instance_Activity ON MultiInstanceExecution(ProcessInstanceId, ActivityId);

CREATE TABLE Users (
    Id NVARCHAR(64) PRIMARY KEY,
    Username NVARCHAR(200) NOT NULL,
    Email NVARCHAR(400) NOT NULL,
    IsActive BIT NOT NULL,
    Roles NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    LastModified DATETIME2 NOT NULL
);
CREATE INDEX IX_Users_Username ON Users(Username);
CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Users_IsActive ON Users(IsActive);
CREATE INDEX IX_Users_CreatedAt ON Users(CreatedAt);

CREATE TABLE DecisionDefinitions (
    Id NVARCHAR(200) PRIMARY KEY,
    [Key] NVARCHAR(200) NOT NULL,
    Name NVARCHAR(500) NOT NULL,
    DmnXml NVARCHAR(MAX) NOT NULL,
    TenantId NVARCHAR(64) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_DecisionDefinition_Key_Tenant ON DecisionDefinitions([Key], TenantId);
CREATE INDEX IX_DecisionDefinition_Key ON DecisionDefinitions([Key]);

CREATE TABLE DecisionInstances (
    Id NVARCHAR(100) PRIMARY KEY,
    DecisionDefinitionKey NVARCHAR(200) NOT NULL FOREIGN KEY REFERENCES DecisionDefinitions([Key]) ON DELETE CASCADE,
    EvaluationTime DATETIME2 NOT NULL,
    TenantId NVARCHAR(64) NULL,
    ErrorMessage NVARCHAR(2000) NULL,
    InputVariables NVARCHAR(MAX) NOT NULL,
    OutputVariables NVARCHAR(MAX) NOT NULL
);
CREATE INDEX IX_DecisionInstance_Key ON DecisionInstances(DecisionDefinitionKey);
CREATE INDEX IX_DecisionInstance_Tenant ON DecisionInstances(TenantId);
CREATE INDEX IX_DecisionInstance_Time ON DecisionInstances(EvaluationTime);
CREATE INDEX IX_DecisionInstance_Key_Tenant ON DecisionInstances(DecisionDefinitionKey, TenantId);

CREATE TABLE DmnDecisionTables (
    [Key] NVARCHAR(200) PRIMARY KEY,
    Name NVARCHAR(500) NULL,
    HitPolicy NVARCHAR(50) NULL,
    Inputs NVARCHAR(MAX) NOT NULL,
    Outputs NVARCHAR(MAX) NOT NULL,
    Rules NVARCHAR(MAX) NOT NULL
);
CREATE INDEX IX_DmnDecisionTables_Name ON DmnDecisionTables(Name);

CREATE TABLE Tenants (
    Id NVARCHAR(100) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL
);
CREATE INDEX IX_Tenants_Name ON Tenants(Name);

CREATE TABLE SimulationScenarios (
    Id NVARCHAR(100) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(1000) NULL,
    ProcessDefinitionId NVARCHAR(255) NULL,
    BpmnXml NVARCHAR(MAX) NULL,
    MaxSteps INT NOT NULL,
    TenantId NVARCHAR(64) NULL
);
CREATE INDEX IX_SimScenarios_Tenant ON SimulationScenarios(TenantId);
CREATE INDEX IX_SimScenarios_Name ON SimulationScenarios(Name);

CREATE TABLE ProcessMiningEvents (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventType NVARCHAR(200) NOT NULL,
    ProcessInstanceId NVARCHAR(100) NOT NULL,
    TaskId NVARCHAR(100) NULL,
    ActivityId NVARCHAR(100) NULL,
    UserId NVARCHAR(100) NULL,
    TenantId NVARCHAR(64) NULL,
    Timestamp DATETIME2 NOT NULL,
    PayloadJson NVARCHAR(4000) NULL
);
CREATE INDEX IX_PME_EventType ON ProcessMiningEvents(EventType);
CREATE INDEX IX_PME_Instance ON ProcessMiningEvents(ProcessInstanceId);
CREATE INDEX IX_PME_Tenant ON ProcessMiningEvents(TenantId);
CREATE INDEX IX_PME_Timestamp ON ProcessMiningEvents([Timestamp]);

-- Optional seed (comment out in production pipelines)
-- INSERT INTO Users(Id, Username, Email, IsActive, Roles, CreatedAt, LastModified) VALUES
-- ('1','admin','admin@example.com',1,'["admin"]',SYSUTCDATETIME(),SYSUTCDATETIME()),
-- ('2','user1','user1@example.com',1,'["user"]',SYSUTCDATETIME(),SYSUTCDATETIME()),
-- ('3','user2','user2@example.com',1,'["user"]',SYSUTCDATETIME(),SYSUTCDATETIME());
