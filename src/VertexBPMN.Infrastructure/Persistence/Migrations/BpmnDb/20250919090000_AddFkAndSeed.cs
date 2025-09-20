using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFkAndSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EngineDeployments
            migrationBuilder.CreateTable(
                name: "EngineDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngineDeployments", x => x.Id);
                });

            // ProcessDefinitions
            migrationBuilder.CreateTable(
                name: "ProcessDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    BpmnXml = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessDefinitions_EngineDeployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "EngineDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ProcessInstances
            migrationBuilder.CreateTable(
                name: "ProcessInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BusinessKey = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InstanceId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ProcessId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveTasks = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveTokens = table.Column<string>(type: "TEXT", nullable: false),
                    Variables = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessInstances_ProcessDefinitions_ProcessDefinitionId",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "ProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ExecutionTokens
            migrationBuilder.CreateTable(
                name: "ExecutionTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrentNodeId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    NodeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Variables = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AssignedWorker = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionTokens_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Variables
            migrationBuilder.CreateTable(
                name: "Variables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScopeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ProcessInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Variables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Variables_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Jobs
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Retries = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // UserTasks
            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Assignee = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FormKey = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    FormSchema = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // HistoryEvents
            migrationBuilder.CreateTable(
                name: "HistoryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ElementId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Data = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryEvents_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Incidents
            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // MultiInstanceExecutions
            migrationBuilder.CreateTable(
                name: "MultiInstanceExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    InstanceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSequential = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultiInstanceExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MultiInstanceExecutions_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Indexes
            migrationBuilder.CreateIndex(name: "IX_EngineDeployments_CreatedAt", table: "EngineDeployments", column: "CreatedAt");
            migrationBuilder.CreateIndex(name: "IX_EngineDeployments_TenantId", table: "EngineDeployments", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_EngineDeployments_Name", table: "EngineDeployments", column: "Name");
            migrationBuilder.CreateIndex(name: "IX_EngineDeployments_Name_TenantId", table: "EngineDeployments", columns: new[] { "Name", "TenantId" });

            migrationBuilder.CreateIndex(name: "IX_ProcessDefinitions_Key", table: "ProcessDefinitions", column: "Key");
            migrationBuilder.CreateIndex(name: "IX_ProcessDefinitions_Key_Version", table: "ProcessDefinitions", columns: new[] { "Key", "Version" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_ProcessDefinitions_TenantId", table: "ProcessDefinitions", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_ProcessDefinitions_DeploymentId", table: "ProcessDefinitions", column: "DeploymentId");

            migrationBuilder.CreateIndex(name: "IX_ProcessInstances_ProcessDefinitionId", table: "ProcessInstances", column: "ProcessDefinitionId");
            migrationBuilder.CreateIndex(name: "IX_ProcessInstances_BusinessKey", table: "ProcessInstances", column: "BusinessKey");
            migrationBuilder.CreateIndex(name: "IX_ProcessInstances_TenantId", table: "ProcessInstances", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_ProcessInstances_State", table: "ProcessInstances", column: "State");
            migrationBuilder.CreateIndex(name: "IX_ProcessInstances_StartedAt", table: "ProcessInstances", column: "StartedAt");

            migrationBuilder.CreateIndex(name: "IX_ExecutionTokens_ProcessInstanceId", table: "ExecutionTokens", column: "ProcessInstanceId");
            migrationBuilder.CreateIndex(name: "IX_ExecutionTokens_CurrentNodeId", table: "ExecutionTokens", column: "CurrentNodeId");
            migrationBuilder.CreateIndex(name: "IX_ExecutionTokens_State", table: "ExecutionTokens", column: "State");
            migrationBuilder.CreateIndex(name: "IX_ExecutionTokens_AssignedWorker", table: "ExecutionTokens", column: "AssignedWorker");
            migrationBuilder.CreateIndex(name: "IX_ExecutionTokens_CreatedAt", table: "ExecutionTokens", column: "CreatedAt");

            migrationBuilder.CreateIndex(name: "IX_Variables_ScopeId", table: "Variables", column: "ScopeId");
            migrationBuilder.CreateIndex(name: "IX_Variables_Name", table: "Variables", column: "Name");
            migrationBuilder.CreateIndex(name: "IX_Variables_Type", table: "Variables", column: "Type");
            migrationBuilder.CreateIndex(name: "IX_Variables_TenantId", table: "Variables", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_Variables_ProcessInstanceId", table: "Variables", column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(name: "IX_Jobs_ProcessInstanceId", table: "Jobs", column: "ProcessInstanceId");
            migrationBuilder.CreateIndex(name: "IX_Jobs_Type", table: "Jobs", column: "Type");
            migrationBuilder.CreateIndex(name: "IX_Jobs_State", table: "Jobs", column: "State");
            migrationBuilder.CreateIndex(name: "IX_Jobs_DueDate", table: "Jobs", column: "DueDate");
            migrationBuilder.CreateIndex(name: "IX_Jobs_TenantId", table: "Jobs", column: "TenantId");

            migrationBuilder.CreateIndex(name: "IX_Tasks_ProcessInstanceId", table: "Tasks", column: "ProcessInstanceId");
            migrationBuilder.CreateIndex(name: "IX_Tasks_Type", table: "Tasks", column: "Type");
            migrationBuilder.CreateIndex(name: "IX_Tasks_TenantId", table: "Tasks", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_Tasks_Assignee", table: "Tasks", column: "Assignee");

            migrationBuilder.CreateIndex(name: "IX_HistoryEvents_ProcessInstanceId", table: "HistoryEvents", column: "ProcessInstanceId");
            migrationBuilder.CreateIndex(name: "IX_HistoryEvents_EventType", table: "HistoryEvents", column: "EventType");
            migrationBuilder.CreateIndex(name: "IX_HistoryEvents_ElementId", table: "HistoryEvents", column: "ElementId");
            migrationBuilder.CreateIndex(name: "IX_HistoryEvents_Timestamp", table: "HistoryEvents", column: "Timestamp");
            migrationBuilder.CreateIndex(name: "IX_HistoryEvents_TenantId", table: "HistoryEvents", column: "TenantId");

            migrationBuilder.CreateIndex(name: "IX_Incidents_ProcessInstanceId", table: "Incidents", column: "ProcessInstanceId");
            migrationBuilder.CreateIndex(name: "IX_Incidents_Type", table: "Incidents", column: "Type");
            migrationBuilder.CreateIndex(name: "IX_Incidents_State", table: "Incidents", column: "State");
            migrationBuilder.CreateIndex(name: "IX_Incidents_CreatedAt", table: "Incidents", column: "CreatedAt");
            migrationBuilder.CreateIndex(name: "IX_Incidents_TenantId", table: "Incidents", column: "TenantId");

            migrationBuilder.CreateIndex(name: "IX_MultiInstanceExecutions_ProcessInstanceId", table: "MultiInstanceExecutions", column: "ProcessInstanceId");
            migrationBuilder.CreateIndex(name: "IX_MultiInstanceExecutions_ActivityId", table: "MultiInstanceExecutions", column: "ActivityId");
            migrationBuilder.CreateIndex(name: "IX_MultiInstanceExecutions_ProcessInstanceId_ActivityId", table: "MultiInstanceExecutions", columns: new[] { "ProcessInstanceId", "ActivityId" });

            // Seed deterministic data
            var deploymentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var processDefinitionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var processInstanceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var jobId = Guid.Parse("44444444-4444-4444-4444-444444444444");
            var taskId = Guid.Parse("55555555-5555-5555-5555-555555555555");
            var variableId = Guid.Parse("66666666-6666-6666-6666-666666666666");
            var historyEventId = Guid.Parse("77777777-7777-7777-7777-777777777777");
            var incidentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
            var miExecutionId = Guid.Parse("99999999-9999-9999-9999-999999999999");
            var tokenId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var seedTimestamp = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "EngineDeployments",
                columns: new[] { "Id", "Name", "CreatedAt", "TenantId" },
                values: new object[] { deploymentId, "SampleDeployment", seedTimestamp, null });

            migrationBuilder.InsertData(
                table: "ProcessDefinitions",
                columns: new[] { "Id", "Key", "Name", "Version", "BpmnXml", "TenantId", "CreatedAt", "DeploymentId" },
                values: new object[] { processDefinitionId, "SampleProcess", "Sample Process", 1, "<definitions id='SampleProcess'></definitions>", null, seedTimestamp, deploymentId });

            migrationBuilder.InsertData(
                table: "ProcessInstances",
                columns: new[] { "Id", "ProcessDefinitionId", "BusinessKey", "TenantId", "StartedAt", "EndedAt", "State", "InstanceId", "ProcessId", "Status", "ActiveTasks", "ActiveTokens", "Variables", "CreatedAt", "LastModified" },
                values: new object[] { processInstanceId, processDefinitionId, "BK-001", null, seedTimestamp, null, "Running", "sample-instance-1", "SampleProcess", 0, "[]", "[]", "{}", seedTimestamp, seedTimestamp });

            migrationBuilder.InsertData(
                table: "Jobs",
                columns: new[] { "Id", "ProcessInstanceId", "Type", "DueDate", "Retries", "ErrorMessage", "TenantId", "State", "Payload" },
                values: new object[] { jobId, processInstanceId, "timer", seedTimestamp.AddHours(1), 3, null, null, "Scheduled", null });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "Id", "ProcessInstanceId", "Name", "Type", "Assignee", "TenantId", "CreatedAt", "CompletedAt", "DueDate", "FormKey", "FormSchema" },
                values: new object[] { taskId, processInstanceId, "Review Request", "userTask", null, null, seedTimestamp, null, seedTimestamp.AddDays(2), null, null });

            migrationBuilder.InsertData(
                table: "Variables",
                columns: new[] { "Id", "ScopeId", "Name", "Type", "Value", "TenantId", "ProcessInstanceId", "CreatedAt" },
                values: new object[] { variableId, processInstanceId, "approvalRequired", "boolean", "true", null, processInstanceId, seedTimestamp });

            migrationBuilder.InsertData(
                table: "HistoryEvents",
                columns: new[] { "Id", "ProcessInstanceId", "EventType", "Timestamp", "Details", "TenantId", "ElementId", "Data" },
                values: new object[] { historyEventId, processInstanceId, "PROCESS_STARTED", seedTimestamp, "Process instance started.", null, "startEvent1", null });

            migrationBuilder.InsertData(
                table: "Incidents",
                columns: new[] { "Id", "ProcessInstanceId", "Type", "Message", "CreatedAt", "TenantId", "State" },
                values: new object[] { incidentId, processInstanceId, "None", "No incident", seedTimestamp, null, "Resolved" });

            migrationBuilder.InsertData(
                table: "MultiInstanceExecutions",
                columns: new[] { "Id", "ProcessInstanceId", "ActivityId", "InstanceCount", "CompletedCount", "IsSequential" },
                values: new object[] { miExecutionId, processInstanceId, "activity_multi_1", 3, 0, true });

            migrationBuilder.InsertData(
                table: "ExecutionTokens",
                columns: new[] { "Id", "ProcessInstanceId", "CurrentNodeId", "NodeType", "Variables", "CreatedAt", "AssignedWorker", "AssignedAt", "RetryCount", "State" },
                values: new object[] { tokenId, processInstanceId, "startEvent1", "startEvent", "{}", seedTimestamp, null, null, 0, "Active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ExecutionTokens");
            migrationBuilder.DropTable(name: "HistoryEvents");
            migrationBuilder.DropTable(name: "Incidents");
            migrationBuilder.DropTable(name: "Jobs");
            migrationBuilder.DropTable(name: "MultiInstanceExecutions");
            migrationBuilder.DropTable(name: "Tasks");
            migrationBuilder.DropTable(name: "Variables");
            migrationBuilder.DropTable(name: "ProcessInstances");
            migrationBuilder.DropTable(name: "ProcessDefinitions");
            migrationBuilder.DropTable(name: "EngineDeployments");
        }
    }
}
