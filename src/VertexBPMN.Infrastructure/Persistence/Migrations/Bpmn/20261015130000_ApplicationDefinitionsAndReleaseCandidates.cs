using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable MA0048 // EF migrations use timestamp-prefixed filenames.
namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

[DbContext(typeof(BpmnDbContext))]
[Migration("20261015130000_ApplicationDefinitionsAndReleaseCandidates")]
public sealed class ApplicationDefinitionsAndReleaseCandidates : Migration
{
    private static readonly string[] DefinitionUniqueColumns = new[] { "TenantId", "Key" };
    private static readonly string[] RevisionUniqueColumns = new[] { "ApplicationDefinitionId", "Version" };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("ApplicationDefinitions", table => new
        {
            Id = table.Column<string>(nullable: false), TenantId = table.Column<string>(nullable: false),
            Key = table.Column<string>(nullable: false), Name = table.Column<string>(nullable: false),
            ContentJson = table.Column<string>(nullable: false), Version = table.Column<int>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_ApplicationDefinitions", x => x.Id));
        migrationBuilder.CreateTable("ApplicationRevisions", table => new
        {
            Id = table.Column<string>(nullable: false), ApplicationDefinitionId = table.Column<string>(nullable: false),
            TenantId = table.Column<string>(nullable: false), ContentJson = table.Column<string>(nullable: false),
            ContentHash = table.Column<string>(nullable: false), DiagnosticsJson = table.Column<string>(nullable: false), Version = table.Column<int>(nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_ApplicationRevisions", x => x.Id);
            table.ForeignKey("FK_ApplicationRevisions_ApplicationDefinitions_ApplicationDefinitionId", x => x.ApplicationDefinitionId, "ApplicationDefinitions", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable("ApplicationReleaseCandidates", table => new
        {
            Id = table.Column<string>(nullable: false), TenantId = table.Column<string>(nullable: false),
            ApplicationDefinitionId = table.Column<string>(nullable: false), ApplicationRevisionId = table.Column<string>(nullable: false),
            ManifestJson = table.Column<string>(nullable: false), IsReleased = table.Column<bool>(nullable: false), ReleasedAtTicks = table.Column<long>(nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_ApplicationReleaseCandidates", x => x.Id);
            table.ForeignKey("FK_ApplicationReleaseCandidates_ApplicationRevisions_ApplicationRevisionId", x => x.ApplicationRevisionId, "ApplicationRevisions", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_ApplicationReleaseCandidates_ApplicationRevisionId", "ApplicationReleaseCandidates", "ApplicationRevisionId");
        migrationBuilder.CreateIndex("IX_ApplicationDefinitions_TenantId_Key", "ApplicationDefinitions", DefinitionUniqueColumns, unique: true);
        migrationBuilder.CreateIndex("IX_ApplicationRevisions_ApplicationDefinitionId_Version", "ApplicationRevisions", RevisionUniqueColumns, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ApplicationReleaseCandidates");
        migrationBuilder.DropTable("ApplicationRevisions");
        migrationBuilder.DropTable("ApplicationDefinitions");
    }
}
