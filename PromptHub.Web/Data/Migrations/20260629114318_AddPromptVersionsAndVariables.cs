using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptVersionsAndVariables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromptVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PromptId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    TemplateText = table.Column<string>(type: "text", nullable: false),
                    UsageInstructions = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    ExampleResult = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    ChangeSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptVersions_Prompts_PromptId",
                        column: x => x.PromptId,
                        principalTable: "Prompts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromptVariables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PromptVersionId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    VariableType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Placeholder = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, defaultValue: ""),
                    HelpText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DefaultValue = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptVariables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptVariables_PromptVersions_PromptVersionId",
                        column: x => x.PromptVersionId,
                        principalTable: "PromptVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariables_PromptVersionId_Key",
                table: "PromptVariables",
                columns: new[] { "PromptVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromptVariables_PromptVersionId_SortOrder",
                table: "PromptVariables",
                columns: new[] { "PromptVersionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_PromptId",
                table: "PromptVersions",
                column: "PromptId",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_PromptId_VersionNumber",
                table: "PromptVersions",
                columns: new[] { "PromptId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromptVariables");

            migrationBuilder.DropTable(
                name: "PromptVersions");
        }
    }
}
