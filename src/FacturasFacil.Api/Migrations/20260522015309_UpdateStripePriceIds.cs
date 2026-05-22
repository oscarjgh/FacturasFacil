using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasFacil.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStripePriceIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Planes",
                keyColumn: "Id",
                keyValue: 2,
                column: "StripePriceId",
                value: "price_1TZhohEIUe6HZv681DUf6IjX");

            migrationBuilder.UpdateData(
                table: "Planes",
                keyColumn: "Id",
                keyValue: 3,
                column: "StripePriceId",
                value: "price_1TZhqnEIUe6HZv68vqfRf9g9");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Planes",
                keyColumn: "Id",
                keyValue: 2,
                column: "StripePriceId",
                value: "price_CONTADOR_ID");

            migrationBuilder.UpdateData(
                table: "Planes",
                keyColumn: "Id",
                keyValue: 3,
                column: "StripePriceId",
                value: "price_DESPACHO_ID");
        }
    }
}
