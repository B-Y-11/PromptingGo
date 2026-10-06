using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptHub.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneOtpChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhoneOtpChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    CodeHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvalidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneOtpChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhoneOtpChallenges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneOtpChallenges_ExpiresAtUtc",
                table: "PhoneOtpChallenges",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneOtpChallenges_PhoneNumber_Purpose",
                table: "PhoneOtpChallenges",
                columns: new[] { "PhoneNumber", "Purpose" },
                unique: true,
                filter: "\"ConsumedAtUtc\" IS NULL AND \"InvalidatedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneOtpChallenges_PhoneNumber_Purpose_CreatedAtUtc",
                table: "PhoneOtpChallenges",
                columns: new[] { "PhoneNumber", "Purpose", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneOtpChallenges_UserId",
                table: "PhoneOtpChallenges",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhoneOtpChallenges");
        }
    }
}
