using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class ExternalTaskPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var guidType = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal) ? "uuid" : "TEXT";
            var longType = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal) ? "bigint" : "INTEGER";
            migrationBuilder.AddColumn<Guid>(
                name: "ActivityExecutionId",
                table: "ExecutionTokens",
                type: guidType,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScopeExecutionId",
                table: "ExecutionTokens",
                type: guidType,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalTaskJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: guidType, nullable: false),
                    DefinitionId = table.Column<Guid>(type: guidType, nullable: false),
                    DefinitionVersion = table.Column<int>(nullable: false),
                    ActivityId = table.Column<string>(maxLength: 255, nullable: false),
                    ActivityExecutionId = table.Column<Guid>(type: guidType, nullable: false),
                    WaitTokenId = table.Column<Guid>(type: guidType, nullable: false),
                    ScopeExecutionId = table.Column<Guid>(type: guidType, nullable: false),
                    MultiInstanceExecutionId = table.Column<Guid>(type: guidType, nullable: true),
                    MultiInstanceIndex = table.Column<int>(nullable: true),
                    Topic = table.Column<string>(maxLength: 128, nullable: false),
                    ContractVersion = table.Column<string>(maxLength: 128, nullable: false),
                    AgentProfileRef = table.Column<string>(maxLength: 128, nullable: true),
                    AgentProfileVersion = table.Column<string>(maxLength: 128, nullable: true),
                    InputSnapshot = table.Column<string>(nullable: false),
                    DefinitionSnapshot = table.Column<string>(nullable: false),
                    SchemaSnapshot = table.Column<string>(nullable: false),
                    State = table.Column<string>(maxLength: 32, nullable: false),
                    Revision = table.Column<long>(type: longType, nullable: false),
                    CreatedAt = table.Column<long>(type: longType, nullable: false),
                    AvailableAt = table.Column<long>(type: longType, nullable: false),
                    Deadline = table.Column<long>(type: longType, nullable: false),
                    AttemptsStarted = table.Column<int>(nullable: false),
                    MaxAttempts = table.Column<int>(nullable: false),
                    LeaseId = table.Column<Guid>(type: guidType, nullable: true),
                    LeaseGeneration = table.Column<long>(type: longType, nullable: false),
                    LeaseExpiresAt = table.Column<long>(type: longType, nullable: true),
                    WorkerIssuer = table.Column<string>(nullable: true),
                    WorkerSubject = table.Column<string>(nullable: true),
                    Result = table.Column<string>(nullable: true),
                    CompletionId = table.Column<Guid>(type: guidType, nullable: true),
                    ResultHash = table.Column<string>(maxLength: 64, nullable: true),
                    CompletedAt = table.Column<long>(type: longType, nullable: true),
                    ErrorCode = table.Column<string>(maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalTaskJobs", x => x.Id);
                    table.CheckConstraint("CK_ExternalTaskJobs_Attempts", "\"MaxAttempts\" >= 1 AND \"MaxAttempts\" <= 10 AND \"AttemptsStarted\" >= 0 AND \"AttemptsStarted\" <= \"MaxAttempts\"");
                    table.CheckConstraint("CK_ExternalTaskJobs_Lease", "\"State\" <> 'Leased' OR (\"LeaseId\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL AND \"WorkerIssuer\" IS NOT NULL AND \"WorkerSubject\" IS NOT NULL AND \"LeaseGeneration\" > 0 AND \"AttemptsStarted\" > 0 AND \"LeaseExpiresAt\" <= \"Deadline\")");
                    table.CheckConstraint("CK_ExternalTaskJobs_Revision", "\"Revision\" >= 0 AND \"LeaseGeneration\" >= 0");
                    table.CheckConstraint("CK_ExternalTaskJobs_State", "\"State\" IN ('Ready','Leased','RetryScheduled','Completed','Failed','Cancelled','TimedOut')");
                    table.CheckConstraint("CK_ExternalTaskJobs_Time", "\"Deadline\" > \"CreatedAt\" AND \"AvailableAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_ExternalTaskJobs_ExecutionTokens_WaitTokenId",
                        column: x => x.WaitTokenId,
                        principalTable: "ExecutionTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalTaskJobs_ProcessDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "ProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalTaskJobs_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalTaskAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    JobId = table.Column<Guid>(type: guidType, nullable: false),
                    AttemptNumber = table.Column<int>(nullable: false),
                    LeaseId = table.Column<Guid>(type: guidType, nullable: false),
                    LeaseGeneration = table.Column<long>(type: longType, nullable: false),
                    WorkerIssuer = table.Column<string>(nullable: false),
                    WorkerSubject = table.Column<string>(nullable: false),
                    StartedAt = table.Column<long>(type: longType, nullable: false),
                    EndedAt = table.Column<long>(type: longType, nullable: true),
                    EndReason = table.Column<string>(nullable: true),
                    ErrorCode = table.Column<string>(maxLength: 128, nullable: true),
                    FailureId = table.Column<Guid>(type: guidType, nullable: true),
                    FailureHash = table.Column<string>(maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalTaskAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalTaskAttempts_ExternalTaskJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "ExternalTaskJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalTaskContinuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    JobId = table.Column<Guid>(type: guidType, nullable: false),
                    ActivityExecutionId = table.Column<Guid>(type: guidType, nullable: false),
                    Outcome = table.Column<string>(maxLength: 32, nullable: false),
                    State = table.Column<string>(maxLength: 32, nullable: false),
                    Revision = table.Column<long>(type: longType, nullable: false),
                    CreatedAt = table.Column<long>(type: longType, nullable: false),
                    AppliedAt = table.Column<long>(type: longType, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalTaskContinuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalTaskContinuations_ExternalTaskJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "ExternalTaskJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskAttempts_JobId_AttemptNumber",
                table: "ExternalTaskAttempts",
                columns: new[] { "JobId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskAttempts_JobId_LeaseGeneration",
                table: "ExternalTaskAttempts",
                columns: new[] { "JobId", "LeaseGeneration" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskContinuations_JobId",
                table: "ExternalTaskContinuations",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskContinuations_State_CreatedAt_Id",
                table: "ExternalTaskContinuations",
                columns: new[] { "State", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskJobs_DefinitionId",
                table: "ExternalTaskJobs",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskJobs_ProcessInstanceId",
                table: "ExternalTaskJobs",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskJobs_State_Deadline_Id",
                table: "ExternalTaskJobs",
                columns: new[] { "State", "Deadline", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskJobs_State_LeaseExpiresAt_Id",
                table: "ExternalTaskJobs",
                columns: new[] { "State", "LeaseExpiresAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskJobs_TenantId_ActivityExecutionId",
                table: "ExternalTaskJobs",
                columns: new[] { "TenantId", "ActivityExecutionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskJobs_TenantId_Topic_State_AvailableAt_Id",
                table: "ExternalTaskJobs",
                columns: new[] { "TenantId", "Topic", "State", "AvailableAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaskJobs_WaitTokenId",
                table: "ExternalTaskJobs",
                column: "WaitTokenId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TEMPORARY TABLE ExternalTaskDowngradeGuard (CountValue INTEGER CHECK (CountValue = 0));");
            migrationBuilder.Sql("INSERT INTO ExternalTaskDowngradeGuard SELECT COUNT(*) FROM \"ExternalTaskJobs\";");
            migrationBuilder.Sql("DROP TABLE ExternalTaskDowngradeGuard;");
            migrationBuilder.DropTable(
                name: "ExternalTaskAttempts");

            migrationBuilder.DropTable(
                name: "ExternalTaskContinuations");

            migrationBuilder.DropTable(
                name: "ExternalTaskJobs");

            // Older migrations have no complete target model for EF's SQLite rebuild.
            // Both supported providers implement DROP COLUMN for these unindexed columns.
            migrationBuilder.Sql("ALTER TABLE \"ExecutionTokens\" DROP COLUMN \"ActivityExecutionId\";");
            migrationBuilder.Sql("ALTER TABLE \"ExecutionTokens\" DROP COLUMN \"ScopeExecutionId\";");
        }
    }
}
