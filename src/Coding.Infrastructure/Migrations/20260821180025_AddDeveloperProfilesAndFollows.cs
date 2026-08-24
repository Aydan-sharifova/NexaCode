using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Migrations
{
    /// <inheritdoc />
    public partial class AddDeveloperProfilesAndFollows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeveloperProfiles",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Bio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Headline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GitHubUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrimaryRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExperienceLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsProfilePublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsActivityPublic = table.Column<bool>(type: "boolean", nullable: false),
                    AreFollowersPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeveloperProfiles", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DeveloperProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFollows",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFollows", x => x.ID);
                    table.CheckConstraint("CK_UserFollows_DifferentUsers", "\"FollowerId\" <> \"FollowingId\"");
                    table.ForeignKey(
                        name: "FK_UserFollows_Users_FollowerId",
                        column: x => x.FollowerId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFollows_Users_FollowingId",
                        column: x => x.FollowingId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperProfiles_UserId",
                table: "DeveloperProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFollows_FollowerId_FollowingId",
                table: "UserFollows",
                columns: new[] { "FollowerId", "FollowingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFollows_FollowingId_CreatedAt",
                table: "UserFollows",
                columns: new[] { "FollowingId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeveloperProfiles");

            migrationBuilder.DropTable(
                name: "UserFollows");
        }
    }
}
