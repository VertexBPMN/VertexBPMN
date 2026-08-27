using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class DiagnosePendingBpmnModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventSubscriptions_ProcessInstanceId_ActivityId_State",
                table: "EventSubscriptions");

            migrationBuilder.AddColumn<string>(
                name: "ActiveKey",
                table: "EventSubscriptions",
                type: "TEXT",
                maxLength: 320,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_ActiveKey",
                table: "EventSubscriptions",
                column: "ActiveKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_ProcessInstanceId_ActivityId_State",
                table: "EventSubscriptions",
                columns: new[] { "ProcessInstanceId", "ActivityId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventSubscriptions_ActiveKey",
                table: "EventSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_EventSubscriptions_ProcessInstanceId_ActivityId_State",
                table: "EventSubscriptions");

            migrationBuilder.DropColumn(
                name: "ActiveKey",
                table: "EventSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_ProcessInstanceId_ActivityId_State",
                table: "EventSubscriptions",
                columns: new[] { "ProcessInstanceId", "ActivityId", "State" },
                unique: true);
        }
    }
}
