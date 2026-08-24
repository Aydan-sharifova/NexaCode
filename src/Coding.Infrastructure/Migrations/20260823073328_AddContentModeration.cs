using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentReports",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedModeratorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReports", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ContentReports_Users_AssignedModeratorId",
                        column: x => x.AssignedModeratorId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentReports_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModerationActionRecords",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeratorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreviousState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationActionRecords", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ModerationActionRecords_ContentReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ContentReports",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModerationActionRecords_Users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_AssignedModeratorId",
                table: "ContentReports",
                column: "AssignedModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ReporterId_TargetType_TargetId_State",
                table: "ContentReports",
                columns: new[] { "ReporterId", "TargetType", "TargetId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_State_CreatAt",
                table: "ContentReports",
                columns: new[] { "State", "CreatAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_TargetType_TargetId",
                table: "ContentReports",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActionRecords_ModeratorId",
                table: "ModerationActionRecords",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActionRecords_ReportId_CreatAt",
                table: "ModerationActionRecords",
                columns: new[] { "ReportId", "CreatAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModerationActionRecords");

            migrationBuilder.DropTable(
                name: "ContentReports");
        }
    }
}
