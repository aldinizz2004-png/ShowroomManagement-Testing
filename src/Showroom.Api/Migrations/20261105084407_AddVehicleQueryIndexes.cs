using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Showroom.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_AvailabilityStatus",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Brand_Model",
                table: "Vehicles");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Brand_Status_Price",
                table: "Vehicles",
                columns: new[] { "Brand", "AvailabilityStatus", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Status_Price",
                table: "Vehicles",
                columns: new[] { "AvailabilityStatus", "Price" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Brand_Status_Price",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Status_Price",
                table: "Vehicles");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_AvailabilityStatus",
                table: "Vehicles",
                column: "AvailabilityStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Brand_Model",
                table: "Vehicles",
                columns: new[] { "Brand", "Model" });
        }
    }
}
