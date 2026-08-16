using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Coding.Migrations
{
    /// <inheritdoc />
    public partial class AddProgrammingLanguageCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgrammingLanguages",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgrammingLanguages", x => x.ID);
                });

            migrationBuilder.InsertData(
                table: "ProgrammingLanguages",
                columns: new[] { "ID", "CreatAt", "DeletedAt", "IsActive", "IsDeleted", "Name", "Slug", "SortOrder", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "TypeScript", "typescript", 10, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "C#", "csharp", 20, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Python", "python", 30, null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Java", "java", 40, null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), new DateTime(2026, 8, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Go", "go", 50, null },
                    { new Guid("10000000-0000-0000-0000-000000000006"), new DateTime(2026, 8, 16, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", "other", 1000, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammingLanguages_Name",
                table: "ProgrammingLanguages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammingLanguages_Slug",
                table: "ProgrammingLanguages",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgrammingLanguages");
        }
    }
}
