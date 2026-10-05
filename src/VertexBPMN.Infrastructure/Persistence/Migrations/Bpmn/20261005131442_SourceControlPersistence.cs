using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn
{
    /// <inheritdoc />
    public partial class SourceControlPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The shared snapshot is SQLite-authored. Explicit provider types prevent
            // the migration SQL generator from inheriting SQLite TEXT/INTEGER mappings.
            var postgres = ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL";
            var sqlServer = ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer";
            string ColumnType(Type clr, int? length = null) => clr == typeof(Guid)
                ? postgres ? "uuid" : sqlServer ? "uniqueidentifier" : "TEXT"
                : clr == typeof(long) ? postgres || sqlServer ? "bigint" : "INTEGER"
                : clr == typeof(int) ? postgres ? "integer" : sqlServer ? "int" : "INTEGER"
                : postgres ? length.HasValue ? $"character varying({length})" : "text"
                : sqlServer ? length.HasValue ? $"nvarchar({length})" : "nvarchar(max)" : "TEXT";
            migrationBuilder.CreateTable(
                name: "SourceControlBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: ColumnType(typeof(Guid)), nullable: false),
                    TenantId = table.Column<string>(type: ColumnType(typeof(string), 64), maxLength: 64, nullable: false),
                    Revision = table.Column<long>(type: ColumnType(typeof(long)), nullable: false),
                    BindingJson = table.Column<string>(type: ColumnType(typeof(string)), nullable: false),
                    GrantsJson = table.Column<string>(type: ColumnType(typeof(string)), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceControlBindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceControlOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: ColumnType(typeof(Guid)), nullable: false),
                    RepositoryId = table.Column<Guid>(type: ColumnType(typeof(Guid)), nullable: false),
                    TenantId = table.Column<string>(type: ColumnType(typeof(string), 64), maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: ColumnType(typeof(string), 512), maxLength: 512, nullable: false),
                    Kind = table.Column<int>(type: ColumnType(typeof(int)), nullable: false),
                    IdempotencyKey = table.Column<string>(type: ColumnType(typeof(string), 128), maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: ColumnType(typeof(string), 64), maxLength: 64, nullable: false),
                    ProtectedRequest = table.Column<string>(type: ColumnType(typeof(string)), nullable: false),
                    State = table.Column<int>(type: ColumnType(typeof(int)), nullable: false),
                    UpdatedUtcTicks = table.Column<long>(type: ColumnType(typeof(long)), nullable: false),
                    Fence = table.Column<long>(type: ColumnType(typeof(long)), nullable: false),
                    LeaseOwner = table.Column<string>(type: ColumnType(typeof(string), 128), maxLength: 128, nullable: true),
                    LeaseUntilUtcTicks = table.Column<long>(type: ColumnType(typeof(long)), nullable: true),
                    ErrorCode = table.Column<int>(type: ColumnType(typeof(int)), nullable: true),
                    ProtectedResult = table.Column<string>(type: ColumnType(typeof(string)), nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceControlOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceControlSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: ColumnType(typeof(Guid)), nullable: false),
                    RepositoryId = table.Column<Guid>(type: ColumnType(typeof(Guid)), nullable: false),
                    TenantId = table.Column<string>(type: ColumnType(typeof(string), 64), maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: ColumnType(typeof(string), 512), maxLength: 512, nullable: false),
                    DocumentGeneration = table.Column<Guid>(type: ColumnType(typeof(Guid)), nullable: false),
                    Revision = table.Column<long>(type: ColumnType(typeof(long)), nullable: false),
                    BaseCommit = table.Column<string>(type: ColumnType(typeof(string), 64), maxLength: 64, nullable: false),
                    ExpiresUtcTicks = table.Column<long>(type: ColumnType(typeof(long)), nullable: false),
                    ProtectedSnapshots = table.Column<string>(type: ColumnType(typeof(string)), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceControlSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceControlBindings_TenantId_Id",
                table: "SourceControlBindings",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceControlOperations_State_LeaseUntilUtcTicks",
                table: "SourceControlOperations",
                columns: new[] { "State", "LeaseUntilUtcTicks" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceControlOperations_TenantId_RepositoryId_Kind_IdempotencyKey",
                table: "SourceControlOperations",
                columns: new[] { "TenantId", "RepositoryId", "Kind", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceControlSessions_TenantId_RepositoryId_ExpiresUtcTicks",
                table: "SourceControlSessions",
                columns: new[] { "TenantId", "RepositoryId", "ExpiresUtcTicks" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceControlBindings");

            migrationBuilder.DropTable(
                name: "SourceControlOperations");

            migrationBuilder.DropTable(
                name: "SourceControlSessions");
        }
    }
}
