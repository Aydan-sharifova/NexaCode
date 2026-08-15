using Coding.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260815013000_HideDeletedUserDirectConversations")]
public sealed class HideDeletedUserDirectConversations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Conversations" AS conversation
            SET "IsDeleted" = TRUE,
                "DeletedAt" = CURRENT_TIMESTAMP,
                "UpdateAt" = CURRENT_TIMESTAMP,
                "UpdatedAt" = CURRENT_TIMESTAMP
            WHERE conversation."Type" = 0
              AND conversation."IsDeleted" = FALSE
              AND EXISTS (
                  SELECT 1
                  FROM "ConversationParticipants" AS participant
                  JOIN "Users" AS account ON account."ID" = participant."UserId"
                  WHERE participant."ConversationId" = conversation."ID"
                    AND account."IsDeleted" = TRUE
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Conversations hidden because of deleted accounts must not be restored automatically.
    }
}
