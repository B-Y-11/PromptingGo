using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptMediaConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "PromptMedia",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_Status_StoragePurgedAtUtc_ArchivedAtUtc",
                table: "PromptMedia",
                columns: new[] { "Status", "StoragePurgedAtUtc", "ArchivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptMedia_Status_StoragePurgedAtUtc_ModeratedAtUtc",
                table: "PromptMedia",
                columns: new[] { "Status", "StoragePurgedAtUtc", "ModeratedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromptMedia_Status_StoragePurgedAtUtc_ArchivedAtUtc",
                table: "PromptMedia");

            migrationBuilder.DropIndex(
                name: "IX_PromptMedia_Status_StoragePurgedAtUtc_ModeratedAtUtc",
                table: "PromptMedia");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "PromptMedia");
        }
    }
}
