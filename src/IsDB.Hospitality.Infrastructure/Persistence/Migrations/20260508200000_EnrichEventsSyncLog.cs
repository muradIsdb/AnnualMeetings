using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichEventsSyncLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriggerSource",
                table: "EventsAirSyncLogs",
                type: "text",
                nullable: false,
                defaultValue: "System Auto-Sync");

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatedByStaffId",
                table: "EventsAirSyncLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitiatedByStaffName",
                table: "EventsAirSyncLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordsAdded",
                table: "EventsAirSyncLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecordsUpdated",
                table: "EventsAirSyncLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecordsDeactivated",
                table: "EventsAirSyncLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TravelBookingsSynced",
                table: "EventsAirSyncLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TriggerSource",          table: "EventsAirSyncLogs");
            migrationBuilder.DropColumn(name: "InitiatedByStaffId",     table: "EventsAirSyncLogs");
            migrationBuilder.DropColumn(name: "InitiatedByStaffName",   table: "EventsAirSyncLogs");
            migrationBuilder.DropColumn(name: "RecordsAdded",           table: "EventsAirSyncLogs");
            migrationBuilder.DropColumn(name: "RecordsUpdated",         table: "EventsAirSyncLogs");
            migrationBuilder.DropColumn(name: "RecordsDeactivated",     table: "EventsAirSyncLogs");
            migrationBuilder.DropColumn(name: "TravelBookingsSynced",   table: "EventsAirSyncLogs");
        }
    }
}
