using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveRoomWorkshopTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomInterviewerNotes",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomInterviewerNotes", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RoomInterviewerNotes_LiveCodingRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "LiveCodingRooms",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomInterviewerNotes_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomReactions",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomReactions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RoomReactions_LiveCodingRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "LiveCodingRooms",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomReactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomTasks",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTasks", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RoomTasks_LiveCodingRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "LiveCodingRooms",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomTasks_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomInterviewerNotes_AuthorId",
                table: "RoomInterviewerNotes",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomInterviewerNotes_RoomId_AuthorId_CreatAt",
                table: "RoomInterviewerNotes",
                columns: new[] { "RoomId", "AuthorId", "CreatAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomReactions_RoomId_UserId_Emoji",
                table: "RoomReactions",
                columns: new[] { "RoomId", "UserId", "Emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomReactions_UserId",
                table: "RoomReactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomTasks_CreatedById",
                table: "RoomTasks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RoomTasks_RoomId_CreatAt",
                table: "RoomTasks",
                columns: new[] { "RoomId", "CreatAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomInterviewerNotes");

            migrationBuilder.DropTable(
                name: "RoomReactions");

            migrationBuilder.DropTable(
                name: "RoomTasks");
        }
    }
}
