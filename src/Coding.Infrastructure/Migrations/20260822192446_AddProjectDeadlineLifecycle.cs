using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDeadlineLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_DeadlineAt",
                table: "Projects",
                columns: new[] { "Status", "DeadlineAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Status_DeadlineAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DeadlineAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Projects");
        }
    }
}
