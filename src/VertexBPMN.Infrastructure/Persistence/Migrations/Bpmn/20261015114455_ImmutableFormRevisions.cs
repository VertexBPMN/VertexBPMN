using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0048 // EF migration filenames.

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;
    /// <inheritdoc />
    public partial class ImmutableFormRevisions : Migration
    {
        private static readonly string[] RevisionIndexColumns = ["FormDefinitionId", "Version"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormDefinitionRevisions",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    FormDefinitionId = table.Column<string>(nullable: false),
                    TenantId = table.Column<string>(maxLength: 64, nullable: false),
                    Key = table.Column<string>(maxLength: 128, nullable: false),
                    Name = table.Column<string>(maxLength: 256, nullable: false),
                    Schema = table.Column<string>(nullable: false),
                    SchemaHash = table.Column<string>(maxLength: 64, nullable: false),
                    Version = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDefinitionRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormDefinitionRevisions_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitionRevisions_FormDefinitionId_Version",
                table: "FormDefinitionRevisions",
                columns: RevisionIndexColumns,
                unique: true);
            var hash = ActiveProvider switch
            {
                "Npgsql.EntityFrameworkCore.PostgreSQL" => "encode(sha256(convert_to(\"Schema\", 'UTF8')), 'hex')",
                "Microsoft.EntityFrameworkCore.SqlServer" => "LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varchar(max), [Schema] COLLATE Latin1_General_100_BIN2_UTF8)), 2))",
                _ => "vertex_sha256(\"Schema\")"
            };
            migrationBuilder.Sql($"INSERT INTO \"FormDefinitionRevisions\" (\"Id\",\"FormDefinitionId\",\"TenantId\",\"Key\",\"Name\",\"Schema\",\"SchemaHash\",\"Version\",\"CreatedAt\") SELECT \"Id\",\"Id\",\"TenantId\",\"Key\",\"Name\",\"Schema\",{hash},\"Version\",\"LastModified\" FROM \"FormDefinitions\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormDefinitionRevisions");
        }
    }
