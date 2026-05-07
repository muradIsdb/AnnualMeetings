using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PickupOptionsCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop using EF Core naming convention (SQLite uses this)
            migrationBuilder.Sql(@"
DO $$
BEGIN
    -- Drop whichever FK name exists for PickupDayOptionId
    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_DepartureRequests_PickupDayOptions_PickupDayOptionId'
          AND conrelid = '""DepartureRequests""'::regclass
    ) THEN
        ALTER TABLE ""DepartureRequests"" DROP CONSTRAINT ""FK_DepartureRequests_PickupDayOptions_PickupDayOptionId"";
    ELSIF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'DepartureRequests_PickupDayOptionId_fkey'
          AND conrelid = '""DepartureRequests""'::regclass
    ) THEN
        ALTER TABLE ""DepartureRequests"" DROP CONSTRAINT ""DepartureRequests_PickupDayOptionId_fkey"";
    END IF;

    -- Drop whichever FK name exists for PickupHourOptionId
    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_DepartureRequests_PickupHourOptions_PickupHourOptionId'
          AND conrelid = '""DepartureRequests""'::regclass
    ) THEN
        ALTER TABLE ""DepartureRequests"" DROP CONSTRAINT ""FK_DepartureRequests_PickupHourOptions_PickupHourOptionId"";
    ELSIF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'DepartureRequests_PickupHourOptionId_fkey'
          AND conrelid = '""DepartureRequests""'::regclass
    ) THEN
        ALTER TABLE ""DepartureRequests"" DROP CONSTRAINT ""DepartureRequests_PickupHourOptionId_fkey"";
    END IF;

    -- Recreate both FKs with CASCADE
    ALTER TABLE ""DepartureRequests""
        ADD CONSTRAINT ""FK_DepartureRequests_PickupDayOptions_PickupDayOptionId""
        FOREIGN KEY (""PickupDayOptionId"")
        REFERENCES ""PickupDayOptions""(""Id"")
        ON DELETE CASCADE;

    ALTER TABLE ""DepartureRequests""
        ADD CONSTRAINT ""FK_DepartureRequests_PickupHourOptions_PickupHourOptionId""
        FOREIGN KEY (""PickupHourOptionId"")
        REFERENCES ""PickupHourOptions""(""Id"")
        ON DELETE CASCADE;
END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DepartureRequests_PickupDayOptions_PickupDayOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_DepartureRequests_PickupHourOptions_PickupHourOptionId",
                table: "DepartureRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureRequests_PickupDayOptions_PickupDayOptionId",
                table: "DepartureRequests",
                column: "PickupDayOptionId",
                principalTable: "PickupDayOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureRequests_PickupHourOptions_PickupHourOptionId",
                table: "DepartureRequests",
                column: "PickupHourOptionId",
                principalTable: "PickupHourOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
