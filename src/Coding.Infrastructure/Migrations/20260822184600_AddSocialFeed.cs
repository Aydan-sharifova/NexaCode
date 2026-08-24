using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialPosts",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CodeLanguage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPosts", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SocialPosts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SocialPosts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedSocialPosts",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSocialPosts", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SavedSocialPosts_SocialPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "SocialPosts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedSocialPosts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SocialPostComments",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPostComments", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SocialPostComments_SocialPostComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "SocialPostComments",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SocialPostComments_SocialPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "SocialPosts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialPostComments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SocialPostReactions",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPostReactions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SocialPostReactions_SocialPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "SocialPosts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialPostReactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SocialPostShares",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPostShares", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SocialPostShares_SocialPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "SocialPosts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialPostShares_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedSocialPosts_PostId",
                table: "SavedSocialPosts",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSocialPosts_UserId_CreatedAt",
                table: "SavedSocialPosts",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedSocialPosts_UserId_PostId",
                table: "SavedSocialPosts",
                columns: new[] { "UserId", "PostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostComments_AuthorId",
                table: "SocialPostComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostComments_ParentCommentId",
                table: "SocialPostComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostComments_PostId_CreatedAt",
                table: "SocialPostComments",
                columns: new[] { "PostId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostReactions_PostId_UserId",
                table: "SocialPostReactions",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostReactions_UserId",
                table: "SocialPostReactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_AuthorId_CreatedAt",
                table: "SocialPosts",
                columns: new[] { "AuthorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_CreatedAt_ID",
                table: "SocialPosts",
                columns: new[] { "CreatedAt", "ID" });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_ProjectId",
                table: "SocialPosts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostShares_PostId_UserId",
                table: "SocialPostShares",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostShares_UserId",
                table: "SocialPostShares",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedSocialPosts");

            migrationBuilder.DropTable(
                name: "SocialPostComments");

            migrationBuilder.DropTable(
                name: "SocialPostReactions");

            migrationBuilder.DropTable(
                name: "SocialPostShares");

            migrationBuilder.DropTable(
                name: "SocialPosts");
        }
    }
}
