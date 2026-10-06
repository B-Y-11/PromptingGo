using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptMediaMetadataModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MetadataModeratedAtUtc",
                table: "PromptMedia",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataModeratedById",
                table: "PromptMedia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataModerationComment",
                table: "PromptMedia",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MetadataSubmittedAtUtc",
                table: "PromptMedia",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingAltText",
                table: "PromptMedia",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingCaption",
                table: "PromptMedia",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_MetadataModeratedById",
                table: "PromptMedia",
                column: "MetadataModeratedById");

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_MetadataSubmittedAtUtc",
                table: "PromptMedia",
                column: "MetadataSubmittedAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_PromptMedia_AspNetUsers_MetadataModeratedById",
                table: "PromptMedia",
                column: "MetadataModeratedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PromptMedia_AspNetUsers_MetadataModeratedById",
                table: "PromptMedia");

            migrationBuilder.DropIndex(
                name: "IX_PromptMedia_MetadataModeratedById",
                table: "PromptMedia");

            migrationBuilder.DropIndex(
                name: "IX_PromptMedia_MetadataSubmittedAtUtc",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "MetadataModeratedAtUtc",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "MetadataModeratedById",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "MetadataModerationComment",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "MetadataSubmittedAtUtc",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "PendingAltText",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "PendingCaption",
                table: "PromptMedia");
        }
    }
}
