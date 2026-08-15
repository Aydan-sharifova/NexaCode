using Coding.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260815010000_AnonymizeSoftDeletedUsers")]
public sealed class AnonymizeSoftDeletedUsers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Users"
            SET "FirstName" = 'Deleted',
                "LastName" = 'User',
                "UserName" = 'deleted-' || replace("ID"::text, '-', ''),
                "Email" = 'deleted-' || replace("ID"::text, '-', '') || '@invalid.local',
                "PasswordHash" = '',
                "AvatarUrl" = NULL,
                "Bio" = NULL,
                "EmailVerifiedAt" = NULL,
                "UpdatedAt" = COALESCE("DeletedAt", CURRENT_TIMESTAMP)
            WHERE "IsDeleted" = TRUE
              AND ("UserName" NOT LIKE 'deleted-%' OR "Email" NOT LIKE 'deleted-%@invalid.local');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Anonymized personal data cannot be reconstructed safely.
    }
}
