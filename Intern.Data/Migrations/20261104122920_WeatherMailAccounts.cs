using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intern.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeatherMailAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeatherLoginChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Redeemed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherLoginChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeatherMailAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeliveryMinutes = table.Column<int>(type: "integer", nullable: false),
                    DailyEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UnsubscribeToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastSentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastTestSentUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherMailAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherLoginChallenges_Email_CreatedUtc",
                table: "WeatherLoginChallenges",
                columns: new[] { "Email", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherMailAccounts_Email",
                table: "WeatherMailAccounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeatherMailAccounts_UnsubscribeToken",
                table: "WeatherMailAccounts",
                column: "UnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherLoginChallenges");

            migrationBuilder.DropTable(
                name: "WeatherMailAccounts");
        }
    }
}
