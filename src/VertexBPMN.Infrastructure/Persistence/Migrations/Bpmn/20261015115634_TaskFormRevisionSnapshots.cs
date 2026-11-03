using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0048 // EF migration filenames.

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn;
    /// <inheritdoc />
    public partial class TaskFormRevisionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormRevisionId",
                table: "Tasks",

                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormSchemaHash",
                table: "Tasks",

                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FormVersion",
                table: "Tasks",

                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_FormRevisionId",
                table: "Tasks",
                column: "FormRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_FormDefinitionRevisions_FormRevisionId",
                table: "Tasks",
                column: "FormRevisionId",
                principalTable: "FormDefinitionRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // A legacy task owns its frozen schema; its original revision is unknown.
            var hash = ActiveProvider switch
            {
                "Npgsql.EntityFrameworkCore.PostgreSQL" => "encode(sha256(convert_to(\"FormSchema\", 'UTF8')), 'hex')",
                "Microsoft.EntityFrameworkCore.SqlServer" => "LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varchar(max), [FormSchema] COLLATE Latin1_General_100_BIN2_UTF8)), 2))",
                _ => "vertex_sha256(\"FormSchema\")"
            };
            migrationBuilder.Sql($"UPDATE \"Tasks\" SET \"FormSchemaHash\" = {hash} WHERE \"FormSchema\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                // Postgres DROP COLUMN removes the dependent index and foreign key
                // on the dropped column automatically.
                migrationBuilder.Sql("ALTER TABLE \"Tasks\" DROP COLUMN \"FormRevisionId\";");
                migrationBuilder.Sql("ALTER TABLE \"Tasks\" DROP COLUMN \"FormSchemaHash\";");
                migrationBuilder.Sql("ALTER TABLE \"Tasks\" DROP COLUMN \"FormVersion\";");
                return;
            }

            // SQLite: EF Core cannot emit DropColumn/DropForeignKey for a column that is
            // referenced by a foreign key (NotSupportedException). FormRevisionId is
            // FK-referenced, so it cannot be dropped on SQLite without a full table rebuild;
            // drop the unindexed, non-FK columns and retain FormRevisionId (with its index
            // and foreign key) as an unavoidable SQLite downgrade limitation.
            migrationBuilder.Sql("ALTER TABLE \"Tasks\" DROP COLUMN \"FormSchemaHash\";");
            migrationBuilder.Sql("ALTER TABLE \"Tasks\" DROP COLUMN \"FormVersion\";");
        }
    }
