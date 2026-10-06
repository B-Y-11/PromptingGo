using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptVersionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModeratedAtUtc",
                table: "PromptVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeratedById",
                table: "PromptVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationComment",
                table: "PromptVersions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAtUtc",
                table: "PromptVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PromptVersions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAtUtc",
                table: "PromptVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "PromptVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_ModeratedById",
                table: "PromptVersions",
                column: "ModeratedById");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_PromptId_Status",
                table: "PromptVersions",
                columns: new[] { "PromptId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_PromptVersions_AspNetUsers_ModeratedById",
                table: "PromptVersions",
                column: "ModeratedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(
    """
    UPDATE "PromptVersions" AS version
    SET
        "Status" =
            CASE prompt."Status"
                WHEN 'PendingModeration'
                    THEN 'PendingModeration'
                WHEN 'Published'
                    THEN 'Published'
                WHEN 'Rejected'
                    THEN 'Rejected'
                WHEN 'Archived'
                    THEN 'Published'
                ELSE 'Draft'
            END,

        "UpdatedAtUtc" =
            prompt."UpdatedAtUtc",

        "SubmittedAtUtc" =
            prompt."SubmittedAtUtc",

        "ModeratedAtUtc" =
            prompt."ModeratedAtUtc",

        "PublishedAtUtc" =
            prompt."PublishedAtUtc",

        "ModerationComment" =
            prompt."ModerationComment",

        "ModeratedById" =
            prompt."ModeratedById"

    FROM "Prompts" AS prompt
    WHERE version."PromptId" =
          prompt."Id";
    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PromptVersions_AspNetUsers_ModeratedById",
                table: "PromptVersions");

            migrationBuilder.DropIndex(
                name: "IX_PromptVersions_ModeratedById",
                table: "PromptVersions");

            migrationBuilder.DropIndex(
                name: "IX_PromptVersions_PromptId_Status",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "ModeratedAtUtc",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "ModeratedById",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "ModerationComment",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "PromptVersions");
        }
    }
}
