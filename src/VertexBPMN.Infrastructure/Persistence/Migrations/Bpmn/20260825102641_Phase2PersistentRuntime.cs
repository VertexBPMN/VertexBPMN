using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class Phase2PersistentRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessDefinitions_Key_Version",
                table: "ProcessDefinitions");

            migrationBuilder.DeleteData(
                table: "ExecutionTokens",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.DeleteData(
                table: "HistoryEvents",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"));

            migrationBuilder.DeleteData(
                table: "Incidents",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"));

            migrationBuilder.DeleteData(
                table: "Jobs",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "MultiInstanceExecutions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"));

            migrationBuilder.DeleteData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "Variables",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"));

            migrationBuilder.DeleteData(
                table: "ProcessInstances",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "ProcessDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "EngineDeployments",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<string>(
                name: "ActivityId",
                table: "Tasks",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "Tasks",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "ProcessInstances",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TenantScope",
                table: "ProcessDefinitions",
                maxLength: 128,
                nullable: false,
                defaultValue: "$global");

            migrationBuilder.AddColumn<string>(
                name: "ActivityId",
                table: "Jobs",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Jobs",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Jobs",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "LockOwner",
                table: "Jobs",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntil",
                table: "Jobs",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "Jobs",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ActivityId",
                table: "Incidents",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Incidents",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Incidents",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "ExecutionTokens",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "EventSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProcessInstanceId = table.Column<Guid>(nullable: false),
                    ExecutionTokenId = table.Column<Guid>(nullable: false),
                    ActivityId = table.Column<string>(maxLength: 255, nullable: false),
                    EventType = table.Column<string>(maxLength: 32, nullable: false),
                    EventName = table.Column<string>(maxLength: 255, nullable: false),
                    State = table.Column<string>(maxLength: 32, nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    ConsumedAt = table.Column<DateTime>(nullable: true),
                    Revision = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSubscriptions_ExecutionTokens_ExecutionTokenId",
                        column: x => x.ExecutionTokenId,
                        principalTable: "ExecutionTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventSubscriptions_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeInbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    IdempotencyKey = table.Column<string>(maxLength: 255, nullable: false),
                    Operation = table.Column<string>(maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    TenantScope = table.Column<string>(maxLength: 128, nullable: false),
                    Result = table.Column<string>(nullable: true),
                    ReceivedAt = table.Column<DateTime>(nullable: false),
                    CompletedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeInbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProcessInstanceId = table.Column<Guid>(nullable: false),
                    EventType = table.Column<string>(maxLength: 128, nullable: false),
                    Payload = table.Column<string>(nullable: false),
                    State = table.Column<string>(maxLength: 32, nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    OccurredAt = table.Column<DateTime>(nullable: false),
                    PublishedAt = table.Column<DateTime>(nullable: true),
                    Attempts = table.Column<int>(nullable: false),
                    LockOwner = table.Column<string>(maxLength: 255, nullable: true),
                    LockedUntil = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerRegistrations",
                columns: table => new
                {
                    Id = table.Column<string>(maxLength: 255, nullable: false),
                    HostName = table.Column<string>(maxLength: 255, nullable: false),
                    Port = table.Column<int>(nullable: false),
                    SupportedNodeTypes = table.Column<string>(nullable: false),
                    CurrentLoad = table.Column<int>(nullable: false),
                    MaxCapacity = table.Column<int>(nullable: false),
                    RegisteredAt = table.Column<DateTime>(nullable: false),
                    LastHeartbeat = table.Column<DateTime>(nullable: false),
                    TotalTasksProcessed = table.Column<long>(nullable: false),
                    TotalProcessingMilliseconds = table.Column<double>(nullable: false),
                    CpuUsage = table.Column<double>(nullable: false),
                    MemoryUsage = table.Column<double>(nullable: false),
                    ActiveTasks = table.Column<int>(nullable: false),
                    Revision = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerRegistrations", x => x.Id);
                });

            migrationBuilder.Sql(
                "UPDATE ProcessDefinitions SET TenantScope = CASE " +
                "WHEN TenantId IS NULL OR trim(TenantId) = '' THEN '$global' " +
                "ELSE TenantId END;");

            migrationBuilder.Sql(
                "UPDATE Jobs SET CreatedAt = DueDate WHERE CreatedAt = '0001-01-01 00:00:00';");

            migrationBuilder.CreateIndex(
                name: "IX_Variables_ProcessInstanceId_ScopeId_Name",
                table: "Variables",
                columns: new[] { "ProcessInstanceId", "ScopeId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProcessInstanceId_ActivityId_Status",
                table: "Tasks",
                columns: new[] { "ProcessInstanceId", "ActivityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_TenantScope_Key_Version",
                table: "ProcessDefinitions",
                columns: new[] { "TenantScope", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_State_DueDate_LockedUntil",
                table: "Jobs",
                columns: new[] { "State", "DueDate", "LockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_EventType_EventName_State_TenantId",
                table: "EventSubscriptions",
                columns: new[] { "EventType", "EventName", "State", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_ExecutionTokenId",
                table: "EventSubscriptions",
                column: "ExecutionTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_ProcessInstanceId_ActivityId_State",
                table: "EventSubscriptions",
                columns: new[] { "ProcessInstanceId", "ActivityId", "State" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeInbox_TenantScope_Operation_IdempotencyKey",
                table: "RuntimeInbox",
                columns: new[] { "TenantScope", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOutbox_ProcessInstanceId",
                table: "RuntimeOutbox",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOutbox_State_OccurredAt_LockedUntil",
                table: "RuntimeOutbox",
                columns: new[] { "State", "OccurredAt", "LockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerRegistrations_LastHeartbeat",
                table: "WorkerRegistrations",
                column: "LastHeartbeat");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventSubscriptions");

            migrationBuilder.DropTable(
                name: "RuntimeInbox");

            migrationBuilder.DropTable(
                name: "RuntimeOutbox");

            migrationBuilder.DropTable(
                name: "WorkerRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_Variables_ProcessInstanceId_ScopeId_Name",
                table: "Variables");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProcessInstanceId_ActivityId_Status",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessDefinitions_TenantScope_Key_Version",
                table: "ProcessDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_State_DueDate_LockedUntil",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "ProcessInstances");

            migrationBuilder.DropColumn(
                name: "TenantScope",
                table: "ProcessDefinitions");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LockOwner",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "ExecutionTokens");

            migrationBuilder.InsertData(
                table: "EngineDeployments",
                columns: new[] { "Id", "CreatedAt", "Name", "TenantId" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SampleDeployment", null });

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
                name: "IX_ProcessDefinitions_Key_Version",
                table: "ProcessDefinitions",
                columns: new[] { "Key", "Version" },
                unique: true);
        }
    }
}
