using Coding.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260815012000_BackfillLegacyEmailVerification")]
public sealed class BackfillLegacyEmailVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Users"
            SET "EmailVerifiedAt" = COALESCE("UpdatedAt", "CreatedAt", CURRENT_TIMESTAMP)
            WHERE "IsDeleted" = FALSE
              AND "EmailVerifiedAt" IS NULL
              AND "CreatedAt" < TIMESTAMPTZ '2026-08-14 21:00:00+00';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Legacy verification state cannot be distinguished safely after backfill.
    }
}
