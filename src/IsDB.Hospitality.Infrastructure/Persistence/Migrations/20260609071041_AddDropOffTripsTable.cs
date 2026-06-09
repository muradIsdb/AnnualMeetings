using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDropOffTripsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "CarClasses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DropOffTrips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VehicleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DriverId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DriverName = table.Column<string>(type: "TEXT", nullable: true),
                    DriverPhone = table.Column<string>(type: "TEXT", nullable: true),
                    CarNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Destination = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    LoggedByStaffId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DropOffTrips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DropOffTrips_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DropOffTrips_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DropOffTrips_StaffUsers_LoggedByStaffId",
                        column: x => x.LoggedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DropOffTrips_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DropOffTrips_DriverId",
                table: "DropOffTrips",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DropOffTrips_GuestId",
                table: "DropOffTrips",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_DropOffTrips_LoggedAt",
                table: "DropOffTrips",
                column: "LoggedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_DropOffTrips_LoggedByStaffId",
                table: "DropOffTrips",
                column: "LoggedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_DropOffTrips_Status_LoggedAt",
                table: "DropOffTrips",
                columns: new[] { "Status", "LoggedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DropOffTrips_VehicleId",
                table: "DropOffTrips",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DropOffTrips");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "CarClasses");
        }
    }
}
