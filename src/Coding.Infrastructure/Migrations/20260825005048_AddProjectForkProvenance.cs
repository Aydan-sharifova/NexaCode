using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectForkProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ForkedFromProjectId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ForkedFromProjectId",
                table: "Projects",
                column: "ForkedFromProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Projects_ForkedFromProjectId",
                table: "Projects",
                column: "ForkedFromProjectId",
                principalTable: "Projects",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Projects_ForkedFromProjectId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ForkedFromProjectId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ForkedFromProjectId",
                table: "Projects");
        }
    }
}
