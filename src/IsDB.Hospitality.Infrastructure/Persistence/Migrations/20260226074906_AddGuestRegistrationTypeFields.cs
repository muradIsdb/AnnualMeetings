using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestRegistrationTypeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistrationTypeId",
                table: "Guests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationTypeName",
                table: "Guests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HotelOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickupDayOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupDayOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickupHourOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupHourOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsSelectedForSync = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFromEventsAir = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationTypes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HotelOptions");

            migrationBuilder.DropTable(
                name: "PickupDayOptions");

            migrationBuilder.DropTable(
                name: "PickupHourOptions");

            migrationBuilder.DropTable(
                name: "RegistrationTypes");

            migrationBuilder.DropColumn(
                name: "RegistrationTypeId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "RegistrationTypeName",
                table: "Guests");
        }
    }
}
