using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScreenshotToCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScreenshotCodeGenerations",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImageFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ImageMediaType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ImageHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Analysis = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    AppTsx = table.Column<string>(type: "text", nullable: false),
                    StylesCss = table.Column<string>(type: "text", nullable: false),
                    PreviewHtml = table.Column<string>(type: "text", nullable: false),
                    TargetSnapshotsJson = table.Column<string>(type: "text", nullable: false),
                    ModelProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenshotCodeGenerations", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ScreenshotCodeGenerations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScreenshotCodeGenerations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScreenshotCodeGenerations_ProjectId_GeneratedAt",
                table: "ScreenshotCodeGenerations",
                columns: new[] { "ProjectId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreenshotCodeGenerations_UserId",
                table: "ScreenshotCodeGenerations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScreenshotCodeGenerations");
        }
    }
}
