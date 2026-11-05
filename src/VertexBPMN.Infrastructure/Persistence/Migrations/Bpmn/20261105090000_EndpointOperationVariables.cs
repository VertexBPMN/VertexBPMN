using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048", Justification = "EF migrations use timestamp-prefixed file names.")]
[DbContext(typeof(BpmnDbContext))]
[Migration("20261105090000_EndpointOperationVariables")]
public sealed class EndpointOperationVariables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(
            name: "VariablesJson",
            table: "ApplicationEndpointOperations",
            type: "TEXT",
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "VariablesJson", table: "ApplicationEndpointOperations");
}
