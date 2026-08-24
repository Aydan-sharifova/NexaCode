using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Migrations
{
    /// <inheritdoc />
    public partial class AddTimedUserBanLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE \"Users\" SET \"Status\" = 'Banned' WHERE \"IsSuspended\" = TRUE;");

            migrationBuilder.CreateTable(
                name: "UserBans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BannedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPermanent = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBans_Users_BannedByUserId",
                        column: x => x.BannedByUserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "ID", "CreatAt", "DeletedAt", "Description", "IsDeleted", "Name", "UpdateAt" },
                values: new object[] { new Guid("66666666-6666-6666-6666-666666666666"), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Built-in moderation role.", false, "Moderator", null });

            migrationBuilder.CreateIndex(
                name: "IX_UserBans_BannedByUserId",
                table: "UserBans",
                column: "BannedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBans_UserId_Status_ExpiresAt",
                table: "UserBans",
                columns: new[] { "UserId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBans");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "ID",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"));

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "Users");
        }
    }
}
