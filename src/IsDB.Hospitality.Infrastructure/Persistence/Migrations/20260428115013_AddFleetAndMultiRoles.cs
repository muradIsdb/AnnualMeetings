using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetAndMultiRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL fix: cannot rename CompletedAt (timestamptz) to UnassignedByStaffId (uuid FK)
            // Must drop and re-add with correct type
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "VehicleAssignments");

            migrationBuilder.AddColumn<Guid>(
                name: "UnassignedByStaffId",
                table: "VehicleAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentAssignmentType",
                table: "Vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentGuestId",
                table: "Vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "Vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentType",
                table: "VehicleAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "VehicleAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnassignedAt",
                table: "VehicleAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Guests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSyncDeactivatedCount",
                table: "EventsAirConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drivers_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StaffUserRoles",
                columns: table => new
                {
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffUserRoles", x => new { x.StaffUserId, x.Role });
                    table.ForeignKey(
                        name: "FK_StaffUserRoles_StaffUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CurrentGuestId",
                table: "Vehicles",
                column: "CurrentGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAssignments_DriverId",
                table: "VehicleAssignments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAssignments_UnassignedByStaffId",
                table: "VehicleAssignments",
                column: "UnassignedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_VehicleId",
                table: "Drivers",
                column: "VehicleId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_VehicleAssignments_Drivers_DriverId",
                table: "VehicleAssignments",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_VehicleAssignments_StaffUsers_UnassignedByStaffId",
                table: "VehicleAssignments",
                column: "UnassignedByStaffId",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_Guests_CurrentGuestId",
                table: "Vehicles",
                column: "CurrentGuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VehicleAssignments_Drivers_DriverId",
                table: "VehicleAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_VehicleAssignments_StaffUsers_UnassignedByStaffId",
                table: "VehicleAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_Guests_CurrentGuestId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropTable(
                name: "StaffUserRoles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_CurrentGuestId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_VehicleAssignments_DriverId",
                table: "VehicleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_VehicleAssignments_UnassignedByStaffId",
                table: "VehicleAssignments");

            migrationBuilder.DropColumn(
                name: "CurrentAssignmentType",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CurrentGuestId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "AssignmentType",
                table: "VehicleAssignments");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "VehicleAssignments");

            migrationBuilder.DropColumn(
                name: "UnassignedAt",
                table: "VehicleAssignments");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "LastSyncDeactivatedCount",
                table: "EventsAirConfigs");

            // Reverse: drop UnassignedByStaffId (uuid) and restore CompletedAt (timestamptz)
            migrationBuilder.DropColumn(
                name: "UnassignedByStaffId",
                table: "VehicleAssignments");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "VehicleAssignments",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
