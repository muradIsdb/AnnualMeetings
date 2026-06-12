using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Increases FlightNumber on the Flights table from varchar(20) to varchar(50)
    /// and RoomNumber on the Guests table from varchar(20) to varchar(50).
    ///
    /// Background: EventsAir free-text flight number entries (e.g. "Saudi Arabian Airlines SV1234")
    /// normalise to strings longer than 20 characters, causing a PostgreSQL 22001 error
    /// (value too long for type character varying(20)) that crashes the entire sync.
    ///
    /// Uses raw SQL ALTER TABLE … TYPE varchar(50) for PostgreSQL compatibility.
    /// The EF model snapshot and GuestConfiguration/FlightConfiguration are also updated
    /// to reflect the new limit.
    /// </summary>
    public partial class IncreaseFlightNumberAndRoomNumberLength : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Increase FlightNumber on Flights table: varchar(20) → varchar(50)
            migrationBuilder.Sql(@"ALTER TABLE ""Flights"" ALTER COLUMN ""FlightNumber"" TYPE varchar(50);");

            // Increase RoomNumber on Guests table: varchar(20) → varchar(50)
            migrationBuilder.Sql(@"ALTER TABLE ""Guests"" ALTER COLUMN ""RoomNumber"" TYPE varchar(50);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert FlightNumber: varchar(50) → varchar(20)
            // NOTE: This will fail if any existing value exceeds 20 characters.
            migrationBuilder.Sql(@"ALTER TABLE ""Flights"" ALTER COLUMN ""FlightNumber"" TYPE varchar(20);");

            // Revert RoomNumber: varchar(50) → varchar(20)
            migrationBuilder.Sql(@"ALTER TABLE ""Guests"" ALTER COLUMN ""RoomNumber"" TYPE varchar(20);");
        }
    }
}
