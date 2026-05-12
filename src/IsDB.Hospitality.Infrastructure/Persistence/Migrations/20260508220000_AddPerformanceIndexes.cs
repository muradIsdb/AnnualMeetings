using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All CREATE INDEX statements are idempotent (IF NOT EXISTS) so this migration
            // is safe to run on a database that already has some of these indexes.

            // ── Guests ──────────────────────────────────────────────────────────────
            // Virtually every guest query starts with WHERE "IsActive" = true
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Guests_IsActive"" ON ""Guests"" (""IsActive"");");

            // Dashboard and FlightTracker filter by GuestStatus
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Guests_Status"" ON ""Guests"" (""Status"");");

            // GetRegistrationTypeStatsQuery groups/filters by InboundStatus
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Guests_InboundStatus"" ON ""Guests"" (""InboundStatus"");");

            // GetGuestsQuery and GetArrivalFlightsQuery ORDER BY IsCritical DESC, LastName ASC
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Guests_IsCritical_LastName"" ON ""Guests"" (""IsCritical"" DESC, ""LastName"" ASC);");

            // ── TravelBookings ───────────────────────────────────────────────────────
            // Used 13 times to split arrival vs departure bookings
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_TravelBookings_IsArrival"" ON ""TravelBookings"" (""IsArrival"");");

            // FK — used in every guest → travel booking join
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_TravelBookings_GuestId"" ON ""TravelBookings"" (""GuestId"");");

            // FK — used in every travel booking → flight join
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_TravelBookings_FlightId"" ON ""TravelBookings"" (""FlightId"");");

            // ── Flights ──────────────────────────────────────────────────────────────
            // FlightTrackerSyncService filters out Landed/Cancelled flights every 5 min
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Flights_Status"" ON ""Flights"" (""Status"");");

            // GetArrivalFlightsQuery sorts by ScheduledArrival
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Flights_ScheduledArrival"" ON ""Flights"" (""ScheduledArrival"");");

            // ── VehicleAssignments ───────────────────────────────────────────────────
            // 8 WHERE IsActive = true filters across dashboard and vehicle commands
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VehicleAssignments_IsActive"" ON ""VehicleAssignments"" (""IsActive"");");

            // FK — used when loading a guest's active vehicle assignment
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VehicleAssignments_GuestId"" ON ""VehicleAssignments"" (""GuestId"");");

            // FK — used when checking whether a vehicle is currently assigned
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VehicleAssignments_VehicleId"" ON ""VehicleAssignments"" (""VehicleId"");");

            // ── Alerts ───────────────────────────────────────────────────────────────
            // Dashboard and alert list filter unresolved alerts
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Alerts_IsResolved"" ON ""Alerts"" (""IsResolved"");");

            // FK — used when loading alerts for a specific guest
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Alerts_GuestId"" ON ""Alerts"" (""GuestId"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Guests_IsActive"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Guests_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Guests_InboundStatus"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Guests_IsCritical_LastName"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_TravelBookings_IsArrival"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_TravelBookings_GuestId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_TravelBookings_FlightId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Flights_Status"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Flights_ScheduledArrival"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_VehicleAssignments_IsActive"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_VehicleAssignments_GuestId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_VehicleAssignments_VehicleId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Alerts_IsResolved"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Alerts_GuestId"";");
        }
    }
}
