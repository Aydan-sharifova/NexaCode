using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDatabaseBlueprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DatabaseProvider",
                table: "Projects",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DatabaseSchemaJson",
                table: "Projects",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DatabaseProvider",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DatabaseSchemaJson",
                table: "Projects");
        }
    }
}
