using Coding.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260810010000_AddPublicUserIdsAndImproveDirectMessaging")]
public sealed class AddPublicUserIdsAndImproveDirectMessaging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PublicId",
            table: "Users",
            type: "character varying(8)",
            maxLength: 8,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE "Users"
            SET "PublicId" = translate(upper(substr(md5("ID"::text), 1, 8)), '01IO', '2345')
            WHERE "PublicId" IS NULL OR "PublicId" = '';
            """);

        migrationBuilder.AlterColumn<string>(
            name: "PublicId",
            table: "Users",
            type: "character varying(8)",
            maxLength: 8,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(8)",
            oldMaxLength: 8,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_PublicId",
            table: "Users",
            column: "PublicId",
            unique: true);

        migrationBuilder.DropIndex(name: "IX_Projects_OwnerId", table: "Projects");
        migrationBuilder.CreateIndex(
            name: "IX_Projects_OwnerId_IsPublic",
            table: "Projects",
            columns: new[] { "OwnerId", "IsPublic" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Projects_OwnerId_IsPublic", table: "Projects");
        migrationBuilder.CreateIndex(name: "IX_Projects_OwnerId", table: "Projects", column: "OwnerId");
        migrationBuilder.DropIndex(name: "IX_Users_PublicId", table: "Users");
        migrationBuilder.DropColumn(name: "PublicId", table: "Users");
    }
}
