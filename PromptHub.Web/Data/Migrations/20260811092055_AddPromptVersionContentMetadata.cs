using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptVersionContentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "PromptVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "PromptVersions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "PromptVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "ru");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "PromptVersions",
                type: "character varying(700)",
                maxLength: 700,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "PromptVersions",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            // -------------------------------------------------------------------------
            // EN: Backfill version metadata from existing prompts.
            // RU: Переносим существующие данные промптов в их версии.
            // -------------------------------------------------------------------------

            migrationBuilder.Sql(
                """
        UPDATE "PromptVersions" AS version
        SET
            "Title" =
                prompt."Title",

            "Summary" =
                prompt."Summary",

            "LanguageCode" =
                prompt."LanguageCode",

            "Difficulty" =
                prompt."Difficulty",

            "CategoryId" =
                prompt."CategoryId"

        FROM "Prompts" AS prompt

        WHERE version."PromptId" =
              prompt."Id";
        """);

            // -------------------------------------------------------------------------
            // EN: Create version-specific tag table before inserting data into it.
            // RU: Сначала создаём таблицу тегов версий, затем заполняем её.
            // -------------------------------------------------------------------------

            migrationBuilder.CreateTable(
                name: "PromptVersionTags",
                columns: table => new
                {
                    PromptVersionId =
                        table.Column<int>(
                            type: "integer",
                            nullable: false),

                    TagId =
                        table.Column<int>(
                            type: "integer",
                            nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_PromptVersionTags",
                        x => new
                        {
                            x.PromptVersionId,
                            x.TagId
                        });

                    table.ForeignKey(
                        name: "FK_PromptVersionTags_PromptVersions_PromptVersionId",
                        column: x =>
                            x.PromptVersionId,
                        principalTable:
                            "PromptVersions",
                        principalColumn:
                            "Id",
                        onDelete:
                            ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_PromptVersionTags_Tags_TagId",
                        column: x =>
                            x.TagId,
                        principalTable:
                            "Tags",
                        principalColumn:
                            "Id",
                        onDelete:
                            ReferentialAction.Restrict);
                });

            // -------------------------------------------------------------------------
            // EN: Copy existing public prompt tags to every existing version.
            // RU: Копируем существующие теги промптов в версии.
            // -------------------------------------------------------------------------

            migrationBuilder.Sql(
                """
        INSERT INTO "PromptVersionTags"
        (
            "PromptVersionId",
            "TagId"
        )

        SELECT
            version."Id",
            promptTag."TagId"

        FROM "PromptVersions" AS version

        INNER JOIN "PromptTags" AS promptTag
            ON promptTag."PromptId" =
               version."PromptId"

        ON CONFLICT DO NOTHING;
        """);

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersions_CategoryId",
                table: "PromptVersions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptVersionTags_TagId",
                table: "PromptVersionTags",
                column: "TagId");

            // -------------------------------------------------------------------------
            // EN: Add FK only after CategoryId has been backfilled.
            // RU: FK категории добавляем только после заполнения настоящих CategoryId.
            // -------------------------------------------------------------------------

            migrationBuilder.AddForeignKey(
                name: "FK_PromptVersions_Categories_CategoryId",
                table: "PromptVersions",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PromptVersions_Categories_CategoryId",
                table: "PromptVersions");

            migrationBuilder.DropTable(
                name: "PromptVersionTags");

            migrationBuilder.DropIndex(
                name: "IX_PromptVersions_CategoryId",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "PromptVersions");
        }
    }
}
