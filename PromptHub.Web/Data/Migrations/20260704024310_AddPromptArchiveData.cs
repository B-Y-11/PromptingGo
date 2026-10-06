using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptArchiveData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Prompts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "Prompts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedById",
                table: "Prompts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_ArchivedById",
                table: "Prompts",
                column: "ArchivedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Prompts_AspNetUsers_ArchivedById",
                table: "Prompts",
                column: "ArchivedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prompts_AspNetUsers_ArchivedById",
                table: "Prompts");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_ArchivedById",
                table: "Prompts");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Prompts");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Prompts");

            migrationBuilder.DropColumn(
                name: "ArchivedById",
                table: "Prompts");
        }
    }
}
