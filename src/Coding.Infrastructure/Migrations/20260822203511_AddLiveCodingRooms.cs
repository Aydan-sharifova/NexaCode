using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveCodingRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiveCodingRooms",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChallengeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ProblemTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    ProblemStatement = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StateVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveCodingRooms", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LiveCodingRooms_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LiveCodingRooms_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomMessages",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomMessages", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RoomMessages_LiveCodingRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "LiveCodingRooms",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomMessages_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomParticipants",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvitedById = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomParticipants", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RoomParticipants_LiveCodingRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "LiveCodingRooms",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomParticipants_Users_InvitedById",
                        column: x => x.InvitedById,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiveCodingRooms_OwnerId_Status_ScheduledAt",
                table: "LiveCodingRooms",
                columns: new[] { "OwnerId", "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveCodingRooms_ProjectId_Status",
                table: "LiveCodingRooms",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomMessages_AuthorId",
                table: "RoomMessages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomMessages_RoomId_SentAt",
                table: "RoomMessages",
                columns: new[] { "RoomId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomParticipants_InvitedById",
                table: "RoomParticipants",
                column: "InvitedById");

            migrationBuilder.CreateIndex(
                name: "IX_RoomParticipants_RoomId_UserId",
                table: "RoomParticipants",
                columns: new[] { "RoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomParticipants_UserId_Status",
                table: "RoomParticipants",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomMessages");

            migrationBuilder.DropTable(
                name: "RoomParticipants");

            migrationBuilder.DropTable(
                name: "LiveCodingRooms");
        }
    }
}
