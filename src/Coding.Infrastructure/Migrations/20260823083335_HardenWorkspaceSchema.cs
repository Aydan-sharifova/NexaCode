using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenWorkspaceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkspaceNodes_SiblingNameLookup",
                table: "WorkspaceNodes");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WorkspaceNodes",
                type: "citext",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "WorkspaceNodes"
                        WHERE NOT "IsDeleted"
                        GROUP BY "ProjectId", "ParentId", lower("Name")
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Active workspace siblings contain case-insensitive duplicate names; resolve them before applying HardenWorkspaceSchema.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_WorkspaceNodes_ActiveRootName",
                table: "WorkspaceNodes",
                columns: new[] { "ProjectId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_WorkspaceNodes_ActiveSiblingName",
                table: "WorkspaceNodes",
                columns: new[] { "ProjectId", "ParentId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"ParentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WorkspaceNodes_ActiveRootName",
                table: "WorkspaceNodes");

            migrationBuilder.DropIndex(
                name: "UX_WorkspaceNodes_ActiveSiblingName",
                table: "WorkspaceNodes");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WorkspaceNodes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceNodes_SiblingNameLookup",
                table: "WorkspaceNodes",
                columns: new[] { "ProjectId", "ParentId", "Name" });
        }
    }
}
