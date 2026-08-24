using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedProjectPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IssueId",
                table: "ProjectTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectMilestones",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TargetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMilestones", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ProjectMilestones_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPlans",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Idea = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlanJson = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    PlanHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPlans", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ProjectPlans_Projects_CreatedProjectId",
                        column: x => x.CreatedProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectPlans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectIssues",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectIssues", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ProjectIssues_ProjectMilestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "ProjectMilestones",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectIssues_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_IssueId",
                table: "ProjectTasks",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIssues_MilestoneId_SortOrder",
                table: "ProjectIssues",
                columns: new[] { "MilestoneId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIssues_ProjectId",
                table: "ProjectIssues",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_ProjectId_SortOrder",
                table: "ProjectMilestones",
                columns: new[] { "ProjectId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlans_CreatedProjectId",
                table: "ProjectPlans",
                column: "CreatedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlans_UserId_CreatedAt",
                table: "ProjectPlans",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_ProjectIssues_IssueId",
                table: "ProjectTasks",
                column: "IssueId",
                principalTable: "ProjectIssues",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_ProjectIssues_IssueId",
                table: "ProjectTasks");

            migrationBuilder.DropTable(
                name: "ProjectIssues");

            migrationBuilder.DropTable(
                name: "ProjectPlans");

            migrationBuilder.DropTable(
                name: "ProjectMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_IssueId",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "IssueId",
                table: "ProjectTasks");
        }
    }
}
