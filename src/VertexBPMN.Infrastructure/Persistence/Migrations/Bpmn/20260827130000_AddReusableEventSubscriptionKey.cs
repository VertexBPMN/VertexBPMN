using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260827130000_AddReusableEventSubscriptionKey")]
public partial class AddReusableEventSubscriptionKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EventSubscriptions_ProcessInstanceId_ActivityId_State",
            table: "EventSubscriptions");

        migrationBuilder.AddColumn<string>(
            name: "ActiveKey",
            table: "EventSubscriptions",
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
