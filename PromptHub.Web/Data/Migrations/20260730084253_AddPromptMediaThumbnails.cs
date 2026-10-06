using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptMediaThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalHeight",
                table: "PromptMedia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalWidth",
                table: "PromptMedia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailContentType",
                table: "PromptMedia",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ThumbnailFileSizeBytes",
                table: "PromptMedia",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThumbnailHeight",
                table: "PromptMedia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailStorageKey",
                table: "PromptMedia",
                type: "character varying(700)",
                maxLength: 700,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThumbnailWidth",
                table: "PromptMedia",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalHeight",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "OriginalWidth",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "ThumbnailContentType",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "ThumbnailFileSizeBytes",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "ThumbnailHeight",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "ThumbnailStorageKey",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "ThumbnailWidth",
                table: "PromptMedia");
        }
    }
}
