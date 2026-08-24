using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreserveBinaryWorkspaceContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS keeps upgrades safe for databases that received the earlier
            // binary-content migration before the marketplace migration was regenerated.
            migrationBuilder.Sql("ALTER TABLE \"FileContents\" ADD COLUMN IF NOT EXISTS \"IsBinary\" boolean NOT NULL DEFAULT FALSE;");
            migrationBuilder.Sql("ALTER TABLE \"FileContents\" ADD COLUMN IF NOT EXISTS \"BinaryContent\" bytea NULL;");
            migrationBuilder.Sql("ALTER TABLE \"FileVersions\" ADD COLUMN IF NOT EXISTS \"IsBinary\" boolean NOT NULL DEFAULT FALSE;");
            migrationBuilder.Sql("ALTER TABLE \"FileVersions\" ADD COLUMN IF NOT EXISTS \"BinaryContent\" bytea NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"FileVersions\" DROP COLUMN IF EXISTS \"BinaryContent\";");
            migrationBuilder.Sql("ALTER TABLE \"FileVersions\" DROP COLUMN IF EXISTS \"IsBinary\";");
            migrationBuilder.Sql("ALTER TABLE \"FileContents\" DROP COLUMN IF EXISTS \"BinaryContent\";");
            migrationBuilder.Sql("ALTER TABLE \"FileContents\" DROP COLUMN IF EXISTS \"IsBinary\";");
        }
    }
}
