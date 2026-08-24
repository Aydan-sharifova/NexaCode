using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaticDeployments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectDeployments",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeployedById = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeployedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDeployments", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ProjectDeployments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDeployments_Users_DeployedById",
                        column: x => x.DeployedById,
                        principalTable: "Users",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDeploymentFiles",
                columns: table => new
                {
                    DeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDeploymentFiles", x => new { x.DeploymentId, x.Path });
                    table.ForeignKey(
                        name: "FK_ProjectDeploymentFiles_ProjectDeployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "ProjectDeployments",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeployments_DeployedById",
                table: "ProjectDeployments",
                column: "DeployedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeployments_ProjectId",
                table: "ProjectDeployments",
                column: "ProjectId",
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeployments_ProjectId_Version",
                table: "ProjectDeployments",
                columns: new[] { "ProjectId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeployments_Slug",
                table: "ProjectDeployments",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectDeploymentFiles");

            migrationBuilder.DropTable(
                name: "ProjectDeployments");
        }
    }
}
