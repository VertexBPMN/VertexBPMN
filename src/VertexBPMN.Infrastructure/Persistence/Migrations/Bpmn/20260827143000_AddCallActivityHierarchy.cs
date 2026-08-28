using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20260827143000_AddCallActivityHierarchy")]
public partial class AddCallActivityHierarchy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CallingActivityId",
            table: "ProcessInstances",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ParentProcessInstanceId",
            table: "ProcessInstances",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProcessInstances_ParentProcessInstanceId",
            table: "ProcessInstances",
            column: "ParentProcessInstanceId");

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProcessInstances_ParentProcessInstanceId",
            table: "ProcessInstances");

        migrationBuilder.DropColumn(
            name: "CallingActivityId",
            table: "ProcessInstances");

        migrationBuilder.DropColumn(
            name: "ParentProcessInstanceId",
            table: "ProcessInstances");
    }
}
