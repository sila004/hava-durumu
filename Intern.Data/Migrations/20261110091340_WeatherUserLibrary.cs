using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intern.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeatherUserLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeatherComparisons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstCity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SecondCity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FirstTemperature = table.Column<double>(type: "double precision", nullable: false),
                    FirstHumidity = table.Column<double>(type: "double precision", nullable: false),
                    FirstApparent = table.Column<double>(type: "double precision", nullable: false),
                    FirstWind = table.Column<double>(type: "double precision", nullable: false),
                    FirstCode = table.Column<int>(type: "integer", nullable: false),
                    SecondTemperature = table.Column<double>(type: "double precision", nullable: false),
                    SecondHumidity = table.Column<double>(type: "double precision", nullable: false),
                    SecondApparent = table.Column<double>(type: "double precision", nullable: false),
                    SecondWind = table.Column<double>(type: "double precision", nullable: false),
                    SecondCode = table.Column<int>(type: "integer", nullable: false),
                    ComparedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherComparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeatherComparisons_WeatherMailAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "WeatherMailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeatherFavorites",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherFavorites", x => new { x.AccountId, x.City });
                    table.ForeignKey(
                        name: "FK_WeatherFavorites_WeatherMailAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "WeatherMailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeatherPagePreferences",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Section = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherPagePreferences", x => new { x.AccountId, x.Section });
                    table.ForeignKey(
                        name: "FK_WeatherPagePreferences_WeatherMailAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "WeatherMailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherComparisons_AccountId_ComparedUtc",
                table: "WeatherComparisons",
                columns: new[] { "AccountId", "ComparedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherComparisons");

            migrationBuilder.DropTable(
                name: "WeatherFavorites");

            migrationBuilder.DropTable(
                name: "WeatherPagePreferences");
        }
    }
}
