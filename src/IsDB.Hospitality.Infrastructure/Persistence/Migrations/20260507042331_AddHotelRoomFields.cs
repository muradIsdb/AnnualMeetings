using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelRoomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualOccupiedGuest",
                table: "HotelOptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ActualOccupiedIsDB",
                table: "HotelOptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContractedRoomsGuest",
                table: "HotelOptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContractedRoomsIsDB",
                table: "HotelOptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualOccupiedGuest",
                table: "HotelOptions");

            migrationBuilder.DropColumn(
                name: "ActualOccupiedIsDB",
                table: "HotelOptions");

            migrationBuilder.DropColumn(
                name: "ContractedRoomsGuest",
                table: "HotelOptions");

            migrationBuilder.DropColumn(
                name: "ContractedRoomsIsDB",
                table: "HotelOptions");
        }
    }
}
