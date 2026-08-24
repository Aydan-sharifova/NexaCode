using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorLearningEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "LearningTopics",
                table: "DeveloperProfiles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string[]>(
                name: "Skills",
                table: "DeveloperProfiles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LearningTopics",
                table: "DeveloperProfiles");

            migrationBuilder.DropColumn(
                name: "Skills",
                table: "DeveloperProfiles");
        }
    }
}
