using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleCurrentKnowledgeGraphSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeGraphSnapshots_ProjectId_IsCurrent",
                table: "KnowledgeGraphSnapshots");

            migrationBuilder.CreateIndex(
                name: "UX_KnowledgeGraphSnapshots_CurrentProject",
                table: "KnowledgeGraphSnapshots",
                column: "ProjectId",
                unique: true,
                filter: "\"IsCurrent\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_KnowledgeGraphSnapshots_CurrentProject",
                table: "KnowledgeGraphSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphSnapshots_ProjectId_IsCurrent",
                table: "KnowledgeGraphSnapshots",
                columns: new[] { "ProjectId", "IsCurrent" });
        }
    }
}
