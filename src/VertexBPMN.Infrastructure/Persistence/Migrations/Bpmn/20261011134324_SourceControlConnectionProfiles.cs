using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0048 // EF migrations require timestamp-prefixed filenames.

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

/// <inheritdoc />
public partial class SourceControlConnectionProfiles : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// The design-time snapshot uses SQLite; deployment providers need their own types.
		var providerTypes = ActiveProvider switch
		{
			"Npgsql.EntityFrameworkCore.PostgreSQL" => new ProfileColumnTypes("uuid", "character varying(64)", "bigint", "text", "boolean"),
			"Microsoft.EntityFrameworkCore.SqlServer" => new ProfileColumnTypes("uniqueidentifier", "nvarchar(64)", "bigint", "nvarchar(max)", "bit"),
			_ => new ProfileColumnTypes("TEXT", "TEXT", "INTEGER", "TEXT", "INTEGER")
		};
		migrationBuilder.CreateTable(
			name: "SourceControlConnectionProfiles",
			columns: table => new
			{
				Id = table.Column<Guid>(type: providerTypes.Identifier, nullable: false),
				TenantId = table.Column<string>(type: providerTypes.Tenant, maxLength: 64, nullable: false),
				Revision = table.Column<long>(type: providerTypes.Revision, nullable: false),
				ProfileJson = table.Column<string>(type: providerTypes.Json, nullable: false),
				IsRevoked = table.Column<bool>(type: providerTypes.Boolean, nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_SourceControlConnectionProfiles", x => new { x.TenantId, x.Id });
			});
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(
			name: "SourceControlConnectionProfiles");
	}

	private sealed record ProfileColumnTypes(string Identifier, string Tenant, string Revision, string Json, string Boolean);
}
