using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VertexBPMN.Infrastructure.Persistence.Migrations.ProcessMiningEvents
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(maxLength: 200, nullable: false),
                    ProcessInstanceId = table.Column<string>(maxLength: 100, nullable: false),
                    TaskId = table.Column<string>(maxLength: 100, nullable: true),
                    ActivityId = table.Column<string>(maxLength: 100, nullable: true),
                    UserId = table.Column<string>(maxLength: 100, nullable: true),
                    TenantId = table.Column<string>(maxLength: 64, nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(nullable: false),
                    PayloadJson = table.Column<string>(maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "Id", "ActivityId", "EventType", "PayloadJson", "ProcessInstanceId", "TaskId", "TenantId", "Timestamp", "UserId" },
                values: new object[,]
                {
                    { 1, "startEvent1", "PROCESS_STARTED", null, "33333333-3333-3333-3333-333333333333", null, "tenant-default", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system" },
                    { 2, "activity_userTask_1", "TASK_CREATED", "{\"name\":\"Review Request\"}", "33333333-3333-3333-3333-333333333333", "55555555-5555-5555-5555-555555555555", "tenant-default", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventType",
                table: "Events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ProcessInstanceId",
                table: "Events",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_TenantId",
                table: "Events",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Timestamp",
                table: "Events",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
