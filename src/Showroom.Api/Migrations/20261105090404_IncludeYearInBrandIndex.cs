using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Showroom.Api.Migrations
{
    /// <inheritdoc />
    public partial class IncludeYearInBrandIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Brand_Status_Price",
                table: "Vehicles");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Brand_Status_Price",
                table: "Vehicles",
                columns: new[] { "Brand", "AvailabilityStatus", "Price" })
                .Annotation("SqlServer:Include", new[] { "ManufacturingYear" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Brand_Status_Price",
                table: "Vehicles");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Brand_Status_Price",
                table: "Vehicles",
                columns: new[] { "Brand", "AvailabilityStatus", "Price" });
        }
    }
}
