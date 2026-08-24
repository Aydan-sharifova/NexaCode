using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutonomousTestAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutonomousTestRuns",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Goal = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MaximumIterations = table.Column<int>(type: "integer", nullable: false),
                    CompletedIterations = table.Column<int>(type: "integer", nullable: false),
                    OriginalSourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalConcurrencyToken = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Analysis = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FinalSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProposedSource = table.Column<string>(type: "text", nullable: true),
                    ProposedSourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SuggestedFix = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ModelProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedFileVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousTestRuns", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AutonomousTestRuns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AutonomousTestRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutonomousTestRuns_WorkspaceNodes_WorkspaceNodeId",
                        column: x => x.WorkspaceNodeId,
                        principalTable: "WorkspaceNodes",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutonomousTestIterations",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GeneratedTestSource = table.Column<string>(type: "text", nullable: false),
                    Stdout = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true),
                    Stderr = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true),
                    ExitCode = table.Column<int>(type: "integer", nullable: true),
                    TimedOut = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    FailureAnalysis = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousTestIterations", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AutonomousTestIterations_AutonomousTestRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AutonomousTestRuns",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousTestIterations_RunId_Number",
                table: "AutonomousTestIterations",
                columns: new[] { "RunId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousTestRuns_ProjectId_StartedAt",
                table: "AutonomousTestRuns",
                columns: new[] { "ProjectId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousTestRuns_UserId",
                table: "AutonomousTestRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousTestRuns_WorkspaceNodeId",
                table: "AutonomousTestRuns",
                column: "WorkspaceNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutonomousTestIterations");

            migrationBuilder.DropTable(
                name: "AutonomousTestRuns");
        }
    }
}
