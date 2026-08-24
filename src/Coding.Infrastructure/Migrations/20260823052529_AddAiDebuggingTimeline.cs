using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiDebuggingTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DebuggingExecutionObservations",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ExitCode = table.Column<int>(type: "integer", nullable: true),
                    TimedOut = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebuggingExecutionObservations", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DebuggingExecutionObservations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DebuggingExecutionObservations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebuggingExecutionObservations_WorkspaceNodes_WorkspaceNode~",
                        column: x => x.WorkspaceNodeId,
                        principalTable: "WorkspaceNodes",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DebuggingIncidents",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StackTrace = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    Stdout = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    Stderr = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    ExitCode = table.Column<int>(type: "integer", nullable: true),
                    TimedOut = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RootCause = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LikelyRegression = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SuggestedFix = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RelevantCommitSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RegressionConfidence = table.Column<int>(type: "integer", nullable: true),
                    ModelProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExecutionObservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebuggingIncidents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DebuggingIncidents_DebuggingExecutionObservations_Execution~",
                        column: x => x.ExecutionObservationId,
                        principalTable: "DebuggingExecutionObservations",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DebuggingIncidents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DebuggingIncidents_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebuggingIncidents_WorkspaceNodes_WorkspaceNodeId",
                        column: x => x.WorkspaceNodeId,
                        principalTable: "WorkspaceNodes",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DebuggingEvidence",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    WorkspaceNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommitSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EvidenceAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebuggingEvidence", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DebuggingEvidence_DebuggingIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "DebuggingIncidents",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingEvidence_IncidentId_Fingerprint",
                table: "DebuggingEvidence",
                columns: new[] { "IncidentId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingExecutionObservations_ProjectId_WorkspaceNodeId_Ex~",
                table: "DebuggingExecutionObservations",
                columns: new[] { "ProjectId", "WorkspaceNodeId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingExecutionObservations_UserId",
                table: "DebuggingExecutionObservations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingExecutionObservations_WorkspaceNodeId",
                table: "DebuggingExecutionObservations",
                column: "WorkspaceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingIncidents_CreatedById",
                table: "DebuggingIncidents",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingIncidents_ExecutionObservationId",
                table: "DebuggingIncidents",
                column: "ExecutionObservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingIncidents_ProjectId_OccurredAt",
                table: "DebuggingIncidents",
                columns: new[] { "ProjectId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingIncidents_WorkspaceNodeId",
                table: "DebuggingIncidents",
                column: "WorkspaceNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DebuggingEvidence");

            migrationBuilder.DropTable(
                name: "DebuggingIncidents");

            migrationBuilder.DropTable(
                name: "DebuggingExecutionObservations");
        }
    }
}
