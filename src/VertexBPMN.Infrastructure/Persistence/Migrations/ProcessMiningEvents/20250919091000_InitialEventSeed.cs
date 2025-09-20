using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Persistence.Migrations.ProcessMiningEvents
{
    public partial class InitialEventSeed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProcessInstanceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ActivityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_Events_EventType", table: "Events", column: "EventType");
            migrationBuilder.CreateIndex(name: "IX_Events_ProcessInstanceId", table: "Events", column: "ProcessInstanceId");
            migrationBuilder.CreateIndex(name: "IX_Events_TenantId", table: "Events", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_Events_Timestamp", table: "Events", column: "Timestamp");

            var seedTime = new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "Id", "EventType", "ProcessInstanceId", "TaskId", "ActivityId", "UserId", "TenantId", "Timestamp", "PayloadJson" },
                values: new object[] { 1, "PROCESS_STARTED", "33333333-3333-3333-3333-333333333333", null, "startEvent1", "system", "tenant-default", seedTime, null });
            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "Id", "EventType", "ProcessInstanceId", "TaskId", "ActivityId", "UserId", "TenantId", "Timestamp", "PayloadJson" },
                values: new object[] { 2, "TASK_CREATED", "33333333-3333-3333-3333-333333333333", "55555555-5555-5555-5555-555555555555", "activity_userTask_1", null, "tenant-default", seedTime.AddMinutes(1), "{\"name\":\"Review Request\"}" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Events");
        }
    }
}
