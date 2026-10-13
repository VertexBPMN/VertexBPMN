using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0048 // EF migrations require timestamp-prefixed filenames.

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

/// <inheritdoc />
public partial class SourceControlExecutionContracts : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		var types = ActiveProvider switch
		{
			"Microsoft.EntityFrameworkCore.SqlServer" => (Version: "int", Target: "nvarchar(max)"),
			"Npgsql.EntityFrameworkCore.PostgreSQL" => (Version: "integer", Target: "text"),
			_ => (Version: "INTEGER", Target: "TEXT")
		};
		migrationBuilder.AddColumn<int>(
			name: "ExecutionContractVersion",
			table: "SourceControlOperations",
			type: types.Version,
			nullable: false,
			defaultValue: 0);

		migrationBuilder.AddColumn<string>(
			name: "ProtectedExecutionTarget",
			table: "SourceControlOperations",
			type: types.Target,
			nullable: true);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(
			name: "ExecutionContractVersion",
			table: "SourceControlOperations");

		migrationBuilder.DropColumn(
			name: "ProtectedExecutionTarget",
			table: "SourceControlOperations");
	}
}
