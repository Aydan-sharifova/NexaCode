using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUiGenerator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUiGenerations",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IncludeSampleData = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Analysis = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    PreviewHtml = table.Column<string>(type: "text", nullable: false),
                    FilesJson = table.Column<string>(type: "text", nullable: false),
                    TargetSnapshotsJson = table.Column<string>(type: "text", nullable: false),
                    ModelProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUiGenerations", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AiUiGenerations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiUiGenerations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUiGenerations_ProjectId_GeneratedAt",
                table: "AiUiGenerations",
                columns: new[] { "ProjectId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUiGenerations_UserId",
                table: "AiUiGenerations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUiGenerations");
        }
    }
}
