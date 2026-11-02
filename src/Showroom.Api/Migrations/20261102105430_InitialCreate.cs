using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Showroom.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Brand = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManufacturingYear = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Mileage = table.Column<int>(type: "int", nullable: false),
                    AvailabilityStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.CheckConstraint("CK_Vehicles_Mileage_NonNegative", "[Mileage] >= 0");
                    table.CheckConstraint("CK_Vehicles_Price_Positive", "[Price] > 0");
                    table.CheckConstraint("CK_Vehicles_Year_Range", "[ManufacturingYear] BETWEEN 1990 AND 2100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_AvailabilityStatus",
                table: "Vehicles",
                column: "AvailabilityStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Brand_Model",
                table: "Vehicles",
                columns: new[] { "Brand", "Model" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vehicles");
        }
    }
}
