using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GitCommits_ProjectId",
                table: "GitCommits");

            migrationBuilder.CreateTable(
                name: "Achievements",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievements", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EvidenceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.ID);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Achievements_AchievementId",
                        column: x => x.AchievementId,
                        principalTable: "Achievements",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "ID", "Category", "Code", "CreatAt", "DeletedAt", "Description", "Icon", "IsActive", "IsDeleted", "Points", "SortOrder", "Title", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Building", "first-project", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Created your first project.", "folder", true, false, 50, 1, "First Project", null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Building", "first-commit", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Created your first verified repository commit.", "commit", true, false, 50, 2, "First Commit", null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Collaboration", "first-pr", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Opened your first pull request.", "pull-request", true, false, 60, 3, "First Pull Request", null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Collaboration", "first-merge", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Authored a pull request that was merged.", "merge", true, false, 80, 4, "First Merge", null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "Delivery", "first-deployment", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Completed a verified deployment.", "rocket", true, false, 100, 5, "First Deployment", null },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "Community", "first-follower", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Earned your first follower.", "user-plus", true, false, 30, 6, "First Follower", null },
                    { new Guid("10000000-0000-0000-0000-000000000007"), "Community", "ten-followers", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Earned ten distinct followers.", "users", true, false, 80, 7, "10 Followers", null },
                    { new Guid("10000000-0000-0000-0000-000000000008"), "Community", "community-contributor", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Contributed posts and helpful comments across multiple days.", "community", true, false, 100, 8, "Community Contributor", null },
                    { new Guid("10000000-0000-0000-0000-000000000009"), "Quality", "bug-hunter", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Submitted three changes-requested reviews on other developers' pull requests.", "bug", true, false, 120, 9, "Bug Hunter", null },
                    { new Guid("10000000-0000-0000-0000-00000000000a"), "AI", "ai-builder", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Completed three bounded AI agent runs.", "sparkles", true, false, 120, 10, "AI Builder", null },
                    { new Guid("10000000-0000-0000-0000-00000000000b"), "Community", "open-source-contributor", new DateTime(2026, 8, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "Merged a contribution into another owner's public project.", "globe", true, false, 150, 11, "Open Source Contributor", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitCommits_ProjectId_CommitHash",
                table: "GitCommits",
                columns: new[] { "ProjectId", "CommitHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Code",
                table: "Achievements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_IsActive_SortOrder",
                table: "Achievements",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_AchievementId",
                table: "UserAchievements",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_UnlockedAt",
                table: "UserAchievements",
                columns: new[] { "UserId", "UnlockedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropTable(
                name: "Achievements");

            migrationBuilder.DropIndex(
                name: "IX_GitCommits_ProjectId_CommitHash",
                table: "GitCommits");

            migrationBuilder.CreateIndex(
                name: "IX_GitCommits_ProjectId",
                table: "GitCommits",
                column: "ProjectId");
        }
    }
}
