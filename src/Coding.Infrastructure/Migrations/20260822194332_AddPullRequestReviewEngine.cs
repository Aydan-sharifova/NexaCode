using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPullRequestReviewEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedBranch",
                table: "Projects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "main");

            migrationBuilder.AddColumn<bool>(
                name: "RequirePassingPullRequestTests",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequiredPullRequestApprovals",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "PullRequests",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    SourceBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceHeadSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetHeadSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                    RequirePassingTests = table.Column<bool>(type: "boolean", nullable: false),
                    TestsPassed = table.Column<bool>(type: "boolean", nullable: true),
                    TestSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MergedById = table.Column<Guid>(type: "uuid", nullable: true),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MergeCommitSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequests", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PullRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PullRequests_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PullRequests_Users_MergedById",
                        column: x => x.MergedById,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PullRequestComments",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PullRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    CommitSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestComments", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PullRequestComments_PullRequests_PullRequestId",
                        column: x => x.PullRequestId,
                        principalTable: "PullRequests",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PullRequestComments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PullRequestComments_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PullRequestReviews",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PullRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    ReviewedSourceSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestReviews", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PullRequestReviews_PullRequests_PullRequestId",
                        column: x => x.PullRequestId,
                        principalTable: "PullRequests",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PullRequestReviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_RequiredPullRequestApprovals",
                table: "Projects",
                sql: "\"RequiredPullRequestApprovals\" BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestComments_AuthorId",
                table: "PullRequestComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestComments_PullRequestId_IsResolved_IsBlocking",
                table: "PullRequestComments",
                columns: new[] { "PullRequestId", "IsResolved", "IsBlocking" });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestComments_ResolvedById",
                table: "PullRequestComments",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReviews_PullRequestId_ReviewerId",
                table: "PullRequestReviews",
                columns: new[] { "PullRequestId", "ReviewerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReviews_ReviewerId",
                table: "PullRequestReviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_AuthorId",
                table: "PullRequests",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_MergedById",
                table: "PullRequests",
                column: "MergedById");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_ProjectId_Number",
                table: "PullRequests",
                columns: new[] { "ProjectId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_ProjectId_SourceBranch",
                table: "PullRequests",
                columns: new[] { "ProjectId", "SourceBranch" },
                unique: true,
                filter: "\"Status\" = 0 AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_ProjectId_Status_UpdatedAt",
                table: "PullRequests",
                columns: new[] { "ProjectId", "Status", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PullRequestComments");

            migrationBuilder.DropTable(
                name: "PullRequestReviews");

            migrationBuilder.DropTable(
                name: "PullRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_RequiredPullRequestApprovals",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProtectedBranch",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RequirePassingPullRequestTests",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RequiredPullRequestApprovals",
                table: "Projects");
        }
    }
}
