using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048", Justification = "EF migrations use timestamp-prefixed file names.")]
[DbContext(typeof(BpmnDbContext))]
[Migration("20261027162500_EndpointAcceptedSnapshot")]
public sealed class EndpointAcceptedSnapshot : Migration
{
    private static readonly string[] Columns =
        ["ApplicationDefinitionId", "ApplicationRevisionId", "ApplicationReleaseId", "BoundProcessDefinitionId"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.AddColumn<string>(column, "ApplicationEndpointOperations", nullable: true);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.DropColumn(column, "ApplicationEndpointOperations");
        }
    }
}
