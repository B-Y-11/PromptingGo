using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PromptId = table.Column<int>(type: "integer", nullable: false),
                    PromptVersionId = table.Column<int>(type: "integer", nullable: true),
                    UploadedById = table.Column<string>(type: "text", nullable: false),
                    MediaType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StorageProvider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    PublicUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExternalUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, defaultValue: ""),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsCover = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReplacesMediaId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModeratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModeratedById = table.Column<string>(type: "text", nullable: true),
                    ModerationComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedById = table.Column<string>(type: "text", nullable: true),
                    ArchiveReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptMedia_AspNetUsers_ArchivedById",
                        column: x => x.ArchivedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PromptMedia_AspNetUsers_ModeratedById",
                        column: x => x.ModeratedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PromptMedia_AspNetUsers_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromptMedia_PromptMedia_ReplacesMediaId",
                        column: x => x.ReplacesMediaId,
                        principalTable: "PromptMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromptMedia_PromptVersions_PromptVersionId",
                        column: x => x.PromptVersionId,
                        principalTable: "PromptVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PromptMedia_Prompts_PromptId",
                        column: x => x.PromptId,
                        principalTable: "Prompts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_ArchivedById",
                table: "PromptMedia",
                column: "ArchivedById");

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_ModeratedById",
                table: "PromptMedia",
                column: "ModeratedById");

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_PromptId_MediaType_Status",
                table: "PromptMedia",
                columns: new[] { "PromptId", "MediaType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_PromptId_Status_SortOrder",
                table: "PromptMedia",
                columns: new[] { "PromptId", "Status", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_PromptVersionId",
                table: "PromptMedia",
                column: "PromptVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_ReplacesMediaId",
                table: "PromptMedia",
                column: "ReplacesMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_Status_CreatedAtUtc",
                table: "PromptMedia",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_UploadedById_CreatedAtUtc",
                table: "PromptMedia",
                columns: new[] { "UploadedById", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptMedia");
        }
    }
}
