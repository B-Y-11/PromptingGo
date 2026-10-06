using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReadyPromptText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TemplateText",
                table: "PromptVersions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "ReadyText",
                table: "PromptVersions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadyText",
                table: "PromptVersions");

            migrationBuilder.AlterColumn<string>(
                name: "TemplateText",
                table: "PromptVersions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "");
        }
    }
}
