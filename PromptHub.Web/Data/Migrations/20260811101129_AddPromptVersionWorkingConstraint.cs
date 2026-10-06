using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptVersionWorkingConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromptVersions_PromptId",
                table: "PromptVersions");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_PromptId",
                table: "PromptVersions",
                column: "PromptId",
                unique: true,
                filter: "\"Status\" IN ('Draft', 'PendingModeration', 'Rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromptVersions_PromptId",
                table: "PromptVersions");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_PromptId",
                table: "PromptVersions",
                column: "PromptId",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");
        }
    }
}
