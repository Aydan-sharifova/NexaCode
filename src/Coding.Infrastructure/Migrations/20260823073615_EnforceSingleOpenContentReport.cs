using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleOpenContentReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentReports_ReporterId_TargetType_TargetId_State",
                table: "ContentReports");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ReporterId_TargetType_TargetId",
                table: "ContentReports",
                columns: new[] { "ReporterId", "TargetType", "TargetId" },
                unique: true,
                filter: "\"State\" IN ('Pending', 'Reviewing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentReports_ReporterId_TargetType_TargetId",
                table: "ContentReports");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ReporterId_TargetType_TargetId_State",
                table: "ContentReports",
                columns: new[] { "ReporterId", "TargetType", "TargetId", "State" });
        }
    }
}
