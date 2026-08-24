using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketplaceItems",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: false),
                    DownloadCount = table.Column<int>(type: "integer", nullable: false),
                    LikeCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceItems", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MarketplaceItems_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketplaceItemVersions",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ManifestJson = table.Column<string>(type: "jsonb", nullable: false),
                    PermissionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Changelog = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceItemVersions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MarketplaceItemVersions_MarketplaceItems_MarketplaceItemId",
                        column: x => x.MarketplaceItemId,
                        principalTable: "MarketplaceItems",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketplaceLikes",
                columns: table => new
                {
                    MarketplaceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceLikes", x => new { x.MarketplaceItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_MarketplaceLikes_MarketplaceItems_MarketplaceItemId",
                        column: x => x.MarketplaceItemId,
                        principalTable: "MarketplaceItems",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketplaceLikes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedMarketplaceItems",
                columns: table => new
                {
                    MarketplaceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedMarketplaceItems", x => new { x.MarketplaceItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_SavedMarketplaceItems_MarketplaceItems_MarketplaceItemId",
                        column: x => x.MarketplaceItemId,
                        principalTable: "MarketplaceItems",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedMarketplaceItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketplaceInstallations",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceItemVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstalledById = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedPermissionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    InstalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceInstallations", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MarketplaceInstallations_MarketplaceItemVersions_Marketplac~",
                        column: x => x.MarketplaceItemVersionId,
                        principalTable: "MarketplaceItemVersions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceInstallations_MarketplaceItems_MarketplaceItemId",
                        column: x => x.MarketplaceItemId,
                        principalTable: "MarketplaceItems",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceInstallations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketplaceInstallations_Users_InstalledById",
                        column: x => x.InstalledById,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceInstallations_InstalledById",
                table: "MarketplaceInstallations",
                column: "InstalledById");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceInstallations_MarketplaceItemId",
                table: "MarketplaceInstallations",
                column: "MarketplaceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceInstallations_MarketplaceItemVersionId",
                table: "MarketplaceInstallations",
                column: "MarketplaceItemVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceInstallations_ProjectId_MarketplaceItemId",
                table: "MarketplaceInstallations",
                columns: new[] { "ProjectId", "MarketplaceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceInstallations_ProjectId_Status",
                table: "MarketplaceInstallations",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceItems_AuthorId_UpdatedAt",
                table: "MarketplaceItems",
                columns: new[] { "AuthorId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceItems_Slug",
                table: "MarketplaceItems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceItems_Status_Category_UpdatedAt",
                table: "MarketplaceItems",
                columns: new[] { "Status", "Category", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceItemVersions_MarketplaceItemId_IsPublished_Publi~",
                table: "MarketplaceItemVersions",
                columns: new[] { "MarketplaceItemId", "IsPublished", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceItemVersions_MarketplaceItemId_Version",
                table: "MarketplaceItemVersions",
                columns: new[] { "MarketplaceItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceLikes_UserId",
                table: "MarketplaceLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedMarketplaceItems_UserId",
                table: "SavedMarketplaceItems",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketplaceInstallations");

            migrationBuilder.DropTable(
                name: "MarketplaceLikes");

            migrationBuilder.DropTable(
                name: "SavedMarketplaceItems");

            migrationBuilder.DropTable(
                name: "MarketplaceItemVersions");

            migrationBuilder.DropTable(
                name: "MarketplaceItems");
        }
    }
}
