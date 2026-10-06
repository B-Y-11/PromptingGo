using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PromptId = table.Column<int>(type: "integer", nullable: false),
                    AuthorId = table.Column<string>(type: "text", nullable: false),
                    ParentCommentId = table.Column<int>(type: "integer", nullable: true),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<string>(type: "text", nullable: true),
                    HiddenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HiddenById = table.Column<string>(type: "text", nullable: true),
                    ModerationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptComments_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromptComments_AspNetUsers_DeletedById",
                        column: x => x.DeletedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PromptComments_AspNetUsers_HiddenById",
                        column: x => x.HiddenById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PromptComments_PromptComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "PromptComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromptComments_Prompts_PromptId",
                        column: x => x.PromptId,
                        principalTable: "Prompts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptComments_AuthorId_CreatedAtUtc",
                table: "PromptComments",
                columns: new[] { "AuthorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptComments_DeletedById",
                table: "PromptComments",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_PromptComments_HiddenById",
                table: "PromptComments",
                column: "HiddenById");

            migrationBuilder.CreateIndex(
                name: "IX_PromptComments_ParentCommentId",
                table: "PromptComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptComments_PromptId_Status_CreatedAtUtc",
                table: "PromptComments",
                columns: new[] { "PromptId", "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptComments");
        }
    }
}
