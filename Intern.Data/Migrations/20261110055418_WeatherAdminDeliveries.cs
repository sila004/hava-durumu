using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intern.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeatherAdminDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeatherMailDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AttemptedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherMailDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeatherMailDeliveries_WeatherMailAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "WeatherMailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherMailDeliveries_AccountId_AttemptedUtc",
                table: "WeatherMailDeliveries",
                columns: new[] { "AccountId", "AttemptedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherMailDeliveries_AttemptedUtc",
                table: "WeatherMailDeliveries",
                column: "AttemptedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherMailDeliveries");
        }
    }
}
