using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSinglePromptMediaCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(
            MigrationBuilder migrationBuilder)
        {
            // -------------------------------------------------------------------------
            // EN: Remove invalid cover flags before creating the unique index.
            // RU: Убираем недопустимые признаки обложки перед созданием индекса.
            // -------------------------------------------------------------------------

            migrationBuilder.Sql(
                """
                UPDATE "PromptMedia"
                SET
                    "IsCover" = FALSE,
                    "UpdatedAtUtc" = CURRENT_TIMESTAMP
                WHERE
                    "IsCover" = TRUE
                    AND
                    (
                        "Status" IN ('Archived', 'Rejected')
                        OR "StoragePurgedAtUtc" IS NOT NULL
                    );
                """);

            // -------------------------------------------------------------------------
            // EN: Keep only the most suitable cover when historical duplicate covers
            //     exist for the same prompt.
            // RU: Оставляем только наиболее подходящую обложку, если в старых данных
            //     у одного промпта оказалось несколько обложек.
            // -------------------------------------------------------------------------

            migrationBuilder.Sql(
                """
                WITH "RankedCovers" AS
                (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY "PromptId"
                            ORDER BY
                                CASE "Status"
                                    WHEN 'Published' THEN 0
                                    WHEN 'PendingModeration' THEN 1
                                    WHEN 'Draft' THEN 2
                                    ELSE 3
                                END,
                                "SortOrder",
                                "CreatedAtUtc",
                                "Id"
                        ) AS "CoverNumber"
                    FROM "PromptMedia"
                    WHERE "IsCover" = TRUE
                )
                UPDATE "PromptMedia" AS media
                SET
                    "IsCover" = FALSE,
                    "UpdatedAtUtc" = CURRENT_TIMESTAMP
                FROM "RankedCovers" AS ranked
                WHERE
                    media."Id" = ranked."Id"
                    AND ranked."CoverNumber" > 1;
                """);

            migrationBuilder.CreateIndex(
                name:
                    "UX_PromptMedia_PromptId_IsCover",
                table:
                    "PromptMedia",
                column:
                    "PromptId",
                unique:
                    true,
                filter:
                    "\"IsCover\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PromptMedia_PromptId_IsCover",
                table: "PromptMedia");
        }
    }
}
