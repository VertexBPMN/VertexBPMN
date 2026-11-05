using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861, MA0048, MA0051, CA1814

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;

    /// <inheritdoc />
    public partial class ApplicationReleaseActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationReleases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OperationId = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicationDefinitionId = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicationRevisionId = table.Column<string>(type: "TEXT", nullable: false),
                    ManifestJson = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DiagnosticsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    PointerRevision = table.Column<long>(type: "INTEGER", nullable: false),
                    BoundProcessDefinitionId = table.Column<string>(type: "TEXT", nullable: false),
                    GitCommitSha = table.Column<string>(type: "TEXT", nullable: false),
                    GitRepository = table.Column<string>(type: "TEXT", nullable: false),
                    PreparedAtTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    DeployStartedAtTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    ActivatedAtTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtTicks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationReleases_ApplicationRevisions_ApplicationRevisionId",
                        column: x => x.ApplicationRevisionId,
                        principalTable: "ApplicationRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationReleases_ApplicationRevisionId",
                table: "ApplicationReleases",
                column: "ApplicationRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationReleases_TenantId_ApplicationDefinitionId",
                table: "ApplicationReleases",
                columns: new[] { "TenantId", "ApplicationDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationReleases_TenantId_ApplicationDefinitionId_OperationId",
                table: "ApplicationReleases",
                columns: new[] { "TenantId", "ApplicationDefinitionId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationReleases");
        }
    }
