using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCodeToNotificationsAndDepartureRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add EventCode column to Notifications table
            migrationBuilder.Sql(@"
                ALTER TABLE ""Notifications""
                    ADD COLUMN IF NOT EXISTS ""EventCode"" text;
            ");

            // Add EventCode column to DepartureRequests table
            migrationBuilder.Sql(@"
                ALTER TABLE ""DepartureRequests""
                    ADD COLUMN IF NOT EXISTS ""EventCode"" text;
            ");

            // Backfill: stamp all existing records with the current active event code
            // (reads from EventsAirConfigs table — the first/only config row)
            migrationBuilder.Sql(@"
                UPDATE ""Notifications""
                SET ""EventCode"" = (SELECT ""EventCode"" FROM ""EventsAirConfigs"" LIMIT 1)
                WHERE ""EventCode"" IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""DepartureRequests""
                SET ""EventCode"" = (SELECT ""EventCode"" FROM ""EventsAirConfigs"" LIMIT 1)
                WHERE ""EventCode"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Notifications"" DROP COLUMN IF EXISTS ""EventCode"";");
            migrationBuilder.Sql(@"ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""EventCode"";");
        }
    }
}
