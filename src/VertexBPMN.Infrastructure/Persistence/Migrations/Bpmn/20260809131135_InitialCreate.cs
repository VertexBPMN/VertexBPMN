using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EngineDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngineDeployments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MigrationExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    MigrationPlanId = table.Column<Guid>(nullable: false),
                    StartedAt = table.Column<DateTime>(nullable: false),
                    Payload = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MigrationPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    Payload = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    Username = table.Column<string>(maxLength: 200, nullable: false),
                    Email = table.Column<string>(maxLength: 400, nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    Roles = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    LastModified = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Key = table.Column<string>(maxLength: 255, nullable: false),
                    Name = table.Column<string>(maxLength: 500, nullable: false),
                    Version = table.Column<int>(nullable: false),
                    BpmnXml = table.Column<string>(nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    DeploymentId = table.Column<Guid>(nullable: false)
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

            migrationBuilder.CreateTable(
                name: "ProcessInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(nullable: false),
                    BusinessKey = table.Column<string>(maxLength: 255, nullable: true),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    StartedAt = table.Column<DateTime>(nullable: false),
                    EndedAt = table.Column<DateTime>(nullable: true),
                    State = table.Column<string>(maxLength: 50, nullable: false),
                    InstanceId = table.Column<string>(maxLength: 255, nullable: false),
                    ProcessId = table.Column<string>(maxLength: 255, nullable: false),
                    Status = table.Column<int>(nullable: false),
                    ActiveTasks = table.Column<string>(nullable: false),
                    ActiveTokens = table.Column<string>(nullable: false),
                    Variables = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    LastModified = table.Column<DateTime>(nullable: false)
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

            migrationBuilder.CreateTable(
                name: "ExecutionTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProcessInstanceId = table.Column<Guid>(nullable: false),
                    CurrentNodeId = table.Column<string>(maxLength: 255, nullable: false),
                    NodeType = table.Column<string>(maxLength: 100, nullable: false),
                    Variables = table.Column<string>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    AssignedWorker = table.Column<string>(maxLength: 255, nullable: true),
                    AssignedAt = table.Column<DateTime>(nullable: true),
                    RetryCount = table.Column<int>(nullable: false),
                    State = table.Column<string>(maxLength: 50, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "HistoryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProcessInstanceId = table.Column<Guid>(nullable: false),
                    EventType = table.Column<string>(maxLength: 100, nullable: false),
                    Timestamp = table.Column<DateTime>(nullable: false),
                    Details = table.Column<string>(maxLength: 4000, nullable: true),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    ElementId = table.Column<string>(maxLength: 255, nullable: false),
                    Data = table.Column<string>(maxLength: 4000, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProcessInstanceId = table.Column<Guid>(nullable: false),
                    Type = table.Column<string>(maxLength: 100, nullable: false),
                    Message = table.Column<string>(maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    State = table.Column<string>(maxLength: 50, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProcessInstanceId = table.Column<Guid>(nullable: false),
                    Type = table.Column<string>(maxLength: 100, nullable: false),
                    DueDate = table.Column<DateTime>(nullable: false),
                    Retries = table.Column<int>(nullable: false),
                    ErrorMessage = table.Column<string>(maxLength: 4000, nullable: true),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    State = table.Column<string>(maxLength: 50, nullable: false),
                    Payload = table.Column<string>(nullable: true)
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

            migrationBuilder.CreateTable(
                name: "MultiInstanceExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProcessInstanceId = table.Column<Guid>(nullable: false),
                    ActivityId = table.Column<string>(maxLength: 255, nullable: false),
                    InstanceCount = table.Column<int>(nullable: false),
                    CompletedCount = table.Column<int>(nullable: false),
                    IsSequential = table.Column<bool>(nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProcessInstanceId = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(maxLength: 500, nullable: false),
                    Type = table.Column<string>(maxLength: 100, nullable: false),
                    Assignee = table.Column<string>(nullable: true),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    CompletedAt = table.Column<DateTime>(nullable: true),
                    DueDate = table.Column<DateTime>(nullable: true),
                    LastModified = table.Column<DateTime>(nullable: false),
                    ModifiedBy = table.Column<string>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    CandidateUsers = table.Column<string>(nullable: false),
                    CandidateRole = table.Column<string>(nullable: false),
                    RequiredFields = table.Column<string>(nullable: false),
                    FormKey = table.Column<string>(maxLength: 255, nullable: true),
                    FormSchema = table.Column<string>(nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Variables",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ScopeId = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(maxLength: 255, nullable: false),
                    Type = table.Column<string>(maxLength: 100, nullable: false),
                    Value = table.Column<string>(nullable: true),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    ProcessInstanceId = table.Column<Guid>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false)
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

            migrationBuilder.InsertData(
                table: "EngineDeployments",
                columns: new[] { "Id", "CreatedAt", "Name", "TenantId" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SampleDeployment", null });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "IsActive", "LastModified", "Roles", "Username" },
                values: new object[,]
                {
                    { "1", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@example.com", true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "[\"admin\"]", "admin" },
                    { "2", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "user1@example.com", true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "[\"user\"]", "user1" },
                    { "3", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "user2@example.com", true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "[\"user\"]", "user2" }
                });

            migrationBuilder.InsertData(
                table: "ProcessDefinitions",
                columns: new[] { "Id", "BpmnXml", "CreatedAt", "DeploymentId", "Key", "Name", "TenantId", "Version" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), "<definitions id='SampleProcess'></definitions>", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("11111111-1111-1111-1111-111111111111"), "SampleProcess", "Sample Process", null, 1 });

            migrationBuilder.InsertData(
                table: "ProcessInstances",
                columns: new[] { "Id", "ActiveTasks", "ActiveTokens", "BusinessKey", "CreatedAt", "EndedAt", "InstanceId", "LastModified", "ProcessDefinitionId", "ProcessId", "StartedAt", "State", "Status", "TenantId", "Variables" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), "[]", "[]", "BK-001", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "sample-instance-1", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("22222222-2222-2222-2222-222222222222"), "SampleProcess", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Running", 0, null, "{}" });

            migrationBuilder.InsertData(
                table: "ExecutionTokens",
                columns: new[] { "Id", "AssignedAt", "AssignedWorker", "CreatedAt", "CurrentNodeId", "NodeType", "ProcessInstanceId", "RetryCount", "State", "Variables" },
                values: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "startEvent1", "startEvent", new Guid("33333333-3333-3333-3333-333333333333"), 0, "Active", "{}" });

            migrationBuilder.InsertData(
                table: "HistoryEvents",
                columns: new[] { "Id", "Data", "Details", "ElementId", "EventType", "ProcessInstanceId", "TenantId", "Timestamp" },
                values: new object[] { new Guid("77777777-7777-7777-7777-777777777777"), null, "Process instance started.", "startEvent1", "PROCESS_STARTED", new Guid("33333333-3333-3333-3333-333333333333"), null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Incidents",
                columns: new[] { "Id", "CreatedAt", "Message", "ProcessInstanceId", "State", "TenantId", "Type" },
                values: new object[] { new Guid("88888888-8888-8888-8888-888888888888"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "No incident", new Guid("33333333-3333-3333-3333-333333333333"), "Resolved", null, "None" });

            migrationBuilder.InsertData(
                table: "Jobs",
                columns: new[] { "Id", "DueDate", "ErrorMessage", "Payload", "ProcessInstanceId", "Retries", "State", "TenantId", "Type" },
                values: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), new DateTime(2025, 1, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, null, new Guid("33333333-3333-3333-3333-333333333333"), 3, "Scheduled", null, "timer" });

            migrationBuilder.InsertData(
                table: "MultiInstanceExecutions",
                columns: new[] { "Id", "ActivityId", "CompletedCount", "InstanceCount", "IsSequential", "ProcessInstanceId" },
                values: new object[] { new Guid("99999999-9999-9999-9999-999999999999"), "activity_multi_1", 0, 3, true, new Guid("33333333-3333-3333-3333-333333333333") });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "Id", "Assignee", "CandidateRole", "CandidateUsers", "CompletedAt", "CreatedAt", "DueDate", "FormKey", "FormSchema", "LastModified", "ModifiedBy", "Name", "ProcessInstanceId", "RequiredFields", "Status", "TenantId", "Type" },
                values: new object[] { new Guid("55555555-5555-5555-5555-555555555555"), null, "", "[]", null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Review Request", new Guid("33333333-3333-3333-3333-333333333333"), "[]", 0, null, "userTask" });

            migrationBuilder.InsertData(
                table: "Variables",
                columns: new[] { "Id", "CreatedAt", "Name", "ProcessInstanceId", "ScopeId", "TenantId", "Type", "Value" },
                values: new object[] { new Guid("66666666-6666-6666-6666-666666666666"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "approvalRequired", new Guid("33333333-3333-3333-3333-333333333333"), new Guid("33333333-3333-3333-3333-333333333333"), null, "boolean", "true" });

            migrationBuilder.CreateIndex(
                name: "IX_EngineDeployments_CreatedAt",
                table: "EngineDeployments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EngineDeployments_Name",
                table: "EngineDeployments",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_EngineDeployments_Name_TenantId",
                table: "EngineDeployments",
                columns: new[] { "Name", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EngineDeployments_TenantId",
                table: "EngineDeployments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionTokens_AssignedWorker",
                table: "ExecutionTokens",
                column: "AssignedWorker");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionTokens_CreatedAt",
                table: "ExecutionTokens",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionTokens_CurrentNodeId",
                table: "ExecutionTokens",
                column: "CurrentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionTokens_ProcessInstanceId",
                table: "ExecutionTokens",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionTokens_State",
                table: "ExecutionTokens",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_ElementId",
                table: "HistoryEvents",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_EventType",
                table: "HistoryEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_ProcessInstanceId",
                table: "HistoryEvents",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_TenantId",
                table: "HistoryEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEvents_Timestamp",
                table: "HistoryEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CreatedAt",
                table: "Incidents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ProcessInstanceId",
                table: "Incidents",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_State",
                table: "Incidents",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_TenantId",
                table: "Incidents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Type",
                table: "Incidents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_DueDate",
                table: "Jobs",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ProcessInstanceId",
                table: "Jobs",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_State",
                table: "Jobs",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_TenantId",
                table: "Jobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Type",
                table: "Jobs",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationExecutions_MigrationPlanId",
                table: "MigrationExecutions",
                column: "MigrationPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationExecutions_StartedAt",
                table: "MigrationExecutions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationPlans_CreatedAt",
                table: "MigrationPlans",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MultiInstanceExecutions_ActivityId",
                table: "MultiInstanceExecutions",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_MultiInstanceExecutions_ProcessInstanceId",
                table: "MultiInstanceExecutions",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_MultiInstanceExecutions_ProcessInstanceId_ActivityId",
                table: "MultiInstanceExecutions",
                columns: new[] { "ProcessInstanceId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_DeploymentId",
                table: "ProcessDefinitions",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_Key",
                table: "ProcessDefinitions",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_Key_Version",
                table: "ProcessDefinitions",
                columns: new[] { "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_TenantId",
                table: "ProcessDefinitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_BusinessKey",
                table: "ProcessInstances",
                column: "BusinessKey");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_ProcessDefinitionId",
                table: "ProcessInstances",
                column: "ProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_StartedAt",
                table: "ProcessInstances",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_State",
                table: "ProcessInstances",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_TenantId",
                table: "ProcessInstances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Assignee",
                table: "Tasks",
                column: "Assignee");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProcessInstanceId",
                table: "Tasks",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId",
                table: "Tasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Type",
                table: "Tasks",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAt",
                table: "Users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Variables_Name",
                table: "Variables",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Variables_ProcessInstanceId",
                table: "Variables",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Variables_ScopeId",
                table: "Variables",
                column: "ScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_Variables_TenantId",
                table: "Variables",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Variables_Type",
                table: "Variables",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionTokens");

            migrationBuilder.DropTable(
                name: "HistoryEvents");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "MigrationExecutions");

            migrationBuilder.DropTable(
                name: "MigrationPlans");

            migrationBuilder.DropTable(
                name: "MultiInstanceExecutions");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Variables");

            migrationBuilder.DropTable(
                name: "ProcessInstances");

            migrationBuilder.DropTable(
                name: "ProcessDefinitions");

            migrationBuilder.DropTable(
                name: "EngineDeployments");
        }
    }
}
