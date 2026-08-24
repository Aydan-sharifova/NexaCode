using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coding.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectKnowledgeGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeGraphSnapshots",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    FileCount = table.Column<int>(type: "integer", nullable: false),
                    NodeCount = table.Column<int>(type: "integer", nullable: false),
                    EdgeCount = table.Column<int>(type: "integer", nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IndexedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeGraphSnapshots", x => x.ID);
                    table.ForeignKey(
                        name: "FK_KnowledgeGraphSnapshots_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeGraphNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Key = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Line = table.Column<int>(type: "integer", nullable: true),
                    Metadata = table.Column<JsonDocument>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeGraphNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeGraphNodes_KnowledgeGraphSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "KnowledgeGraphSnapshots",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeGraphNodes_WorkspaceNodes_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "WorkspaceNodes",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeGraphEdges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeGraphEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeGraphEdges_KnowledgeGraphNodes_FromNodeId",
                        column: x => x.FromNodeId,
                        principalTable: "KnowledgeGraphNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeGraphEdges_KnowledgeGraphNodes_ToNodeId",
                        column: x => x.ToNodeId,
                        principalTable: "KnowledgeGraphNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeGraphEdges_KnowledgeGraphSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "KnowledgeGraphSnapshots",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphEdges_FromNodeId",
                table: "KnowledgeGraphEdges",
                column: "FromNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphEdges_SnapshotId_FromNodeId_ToNodeId_Kind",
                table: "KnowledgeGraphEdges",
                columns: new[] { "SnapshotId", "FromNodeId", "ToNodeId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphEdges_SnapshotId_ToNodeId",
                table: "KnowledgeGraphEdges",
                columns: new[] { "SnapshotId", "ToNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphEdges_ToNodeId",
                table: "KnowledgeGraphEdges",
                column: "ToNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphNodes_SnapshotId_Key",
                table: "KnowledgeGraphNodes",
                columns: new[] { "SnapshotId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphNodes_SnapshotId_Kind",
                table: "KnowledgeGraphNodes",
                columns: new[] { "SnapshotId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphNodes_SnapshotId_SourceFileId",
                table: "KnowledgeGraphNodes",
                columns: new[] { "SnapshotId", "SourceFileId" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphNodes_SourceFileId",
                table: "KnowledgeGraphNodes",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphSnapshots_ProjectId_IsCurrent",
                table: "KnowledgeGraphSnapshots",
                columns: new[] { "ProjectId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGraphSnapshots_ProjectId_Version",
                table: "KnowledgeGraphSnapshots",
                columns: new[] { "ProjectId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeGraphEdges");

            migrationBuilder.DropTable(
                name: "KnowledgeGraphNodes");

            migrationBuilder.DropTable(
                name: "KnowledgeGraphSnapshots");
        }
    }
}
