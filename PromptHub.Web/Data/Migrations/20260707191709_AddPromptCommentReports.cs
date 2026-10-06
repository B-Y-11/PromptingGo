using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptCommentReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptCommentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommentId = table.Column<int>(type: "integer", nullable: false),
                    ReporterId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<string>(type: "text", nullable: true),
                    ResolutionComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptCommentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptCommentReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromptCommentReports_AspNetUsers_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PromptCommentReports_PromptComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "PromptComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptCommentReports_CommentId_Status_CreatedAtUtc",
                table: "PromptCommentReports",
                columns: new[] { "CommentId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptCommentReports_ReporterId_CreatedAtUtc",
                table: "PromptCommentReports",
                columns: new[] { "ReporterId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptCommentReports_ResolvedById",
                table: "PromptCommentReports",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_PromptCommentReports_Status_CreatedAtUtc",
                table: "PromptCommentReports",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptCommentReports");
        }
    }
}
