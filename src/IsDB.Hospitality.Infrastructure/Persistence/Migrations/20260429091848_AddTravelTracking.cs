using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTravelTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Step 1: Drop the old FK and index from Flights ──
            migrationBuilder.DropForeignKey(
                name: "FK_Flights_Guests_GuestId",
                table: "Flights");

            migrationBuilder.DropIndex(
                name: "IX_Flights_GuestId",
                table: "Flights");

            // ── Step 2: Drop columns no longer needed on Flights ──
            migrationBuilder.DropColumn(name: "ArrivalAirportCode", table: "Flights");
            migrationBuilder.DropColumn(name: "DelayReason", table: "Flights");
            migrationBuilder.DropColumn(name: "DepartureAirportCode", table: "Flights");
            migrationBuilder.DropColumn(name: "GuestId", table: "Flights");
            migrationBuilder.DropColumn(name: "IsArrival", table: "Flights");

            // ── Step 3: Rename columns on Flights ──
            migrationBuilder.RenameColumn(name: "Terminal", table: "Flights", newName: "ActualTerminal");
            migrationBuilder.RenameColumn(name: "Gate", table: "Flights", newName: "ActualGate");
            migrationBuilder.RenameColumn(name: "Airline", table: "Flights", newName: "DeparturePortName");

            // ── Step 4: Make ScheduledDeparture/ScheduledArrival non-nullable ──
            // Use raw SQL to handle provider differences properly
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(
                    @"ALTER TABLE ""Flights"" ALTER COLUMN ""ScheduledDeparture"" SET NOT NULL;");
                migrationBuilder.Sql(
                    @"ALTER TABLE ""Flights"" ALTER COLUMN ""ScheduledArrival"" SET NOT NULL;");
            }
            else
            {
                // SQLite doesn't support ALTER COLUMN; the AlterColumn calls are no-ops on SQLite anyway
                migrationBuilder.AlterColumn<DateTime>(
                    name: "ScheduledDeparture",
                    table: "Flights",
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    oldClrType: typeof(DateTime),
                    oldType: "timestamp with time zone",
                    oldNullable: true);

                migrationBuilder.AlterColumn<DateTime>(
                    name: "ScheduledArrival",
                    table: "Flights",
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    oldClrType: typeof(DateTime),
                    oldType: "timestamp with time zone",
                    oldNullable: true);
            }

            // ── Step 5: Add new columns to Flights ──
            migrationBuilder.AddColumn<string>(
                name: "AirlineIataCode", table: "Flights", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AirlineName", table: "Flights", type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ArrivalPortIataCode", table: "Flights", type: "text", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivalPortName", table: "Flights", type: "character varying(100)", maxLength: 100, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeparturePortIataCode", table: "Flights", type: "text", nullable: true);

            // ── Step 6: Create TravelBookings table ──
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // Use raw SQL so column types match the existing PostgreSQL schema exactly
                migrationBuilder.Sql(@"
                    CREATE TABLE ""TravelBookings"" (
                        ""Id""               uuid          NOT NULL,
                        ""GuestId""          uuid          NOT NULL,
                        ""FlightId""         uuid          NOT NULL,
                        ""IsArrival""        boolean       NOT NULL,
                        ""SeatClass""        character varying(50),
                        ""BookingReference"" text,
                        ""AirlineReference"" text,
                        ""Comment""          text,
                        ""BookingNotes""     character varying(1000),
                        ""Itinerary""        text,
                        ""Tickets""          integer,
                        ""Status""           integer       NOT NULL,
                        ""Terminal""         character varying(20),
                        ""Gate""             character varying(20),
                        ""DelayReason""      character varying(500),
                        ""LastSyncedAt""     timestamp with time zone,
                        ""CreatedAt""        timestamp with time zone NOT NULL,
                        ""UpdatedAt""        timestamp with time zone NOT NULL,
                        CONSTRAINT ""PK_TravelBookings"" PRIMARY KEY (""Id""),
                        CONSTRAINT ""FK_TravelBookings_Flights_FlightId""
                            FOREIGN KEY (""FlightId"") REFERENCES ""Flights"" (""Id"") ON DELETE RESTRICT,
                        CONSTRAINT ""FK_TravelBookings_Guests_GuestId""
                            FOREIGN KEY (""GuestId"") REFERENCES ""Guests"" (""Id"") ON DELETE CASCADE
                    );
                ");

                migrationBuilder.Sql(
                    @"CREATE INDEX ""IX_TravelBookings_FlightId"" ON ""TravelBookings"" (""FlightId"");");
                migrationBuilder.Sql(
                    @"CREATE INDEX ""IX_TravelBookings_GuestId""  ON ""TravelBookings"" (""GuestId"");");
            }
            else
            {
                // SQLite path — use the MigrationBuilder API
                migrationBuilder.CreateTable(
                    name: "TravelBookings",
                    columns: table => new
                    {
                        Id = table.Column<Guid>(type: "uuid", nullable: false),
                        GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                        FlightId = table.Column<Guid>(type: "uuid", nullable: false),
                        IsArrival = table.Column<bool>(type: "boolean", nullable: false),
                        SeatClass = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                        BookingReference = table.Column<string>(type: "text", nullable: true),
                        AirlineReference = table.Column<string>(type: "text", nullable: true),
                        Comment = table.Column<string>(type: "text", nullable: true),
                        BookingNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                        Itinerary = table.Column<string>(type: "text", nullable: true),
                        Tickets = table.Column<int>(type: "integer", nullable: true),
                        Status = table.Column<int>(type: "integer", nullable: false),
                        Terminal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                        Gate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                        DelayReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                        LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                        CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                        UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_TravelBookings", x => x.Id);
                        table.ForeignKey(
                            name: "FK_TravelBookings_Flights_FlightId",
                            column: x => x.FlightId,
                            principalTable: "Flights",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict);
                        table.ForeignKey(
                            name: "FK_TravelBookings_Guests_GuestId",
                            column: x => x.GuestId,
                            principalTable: "Guests",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });

                migrationBuilder.CreateIndex(
                    name: "IX_TravelBookings_FlightId", table: "TravelBookings", column: "FlightId");

                migrationBuilder.CreateIndex(
                    name: "IX_TravelBookings_GuestId", table: "TravelBookings", column: "GuestId");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TravelBookings");

            migrationBuilder.DropColumn(name: "AirlineIataCode", table: "Flights");
            migrationBuilder.DropColumn(name: "AirlineName", table: "Flights");
            migrationBuilder.DropColumn(name: "ArrivalPortIataCode", table: "Flights");
            migrationBuilder.DropColumn(name: "ArrivalPortName", table: "Flights");
            migrationBuilder.DropColumn(name: "DeparturePortIataCode", table: "Flights");

            migrationBuilder.RenameColumn(name: "DeparturePortName", table: "Flights", newName: "Airline");
            migrationBuilder.RenameColumn(name: "ActualTerminal", table: "Flights", newName: "Terminal");
            migrationBuilder.RenameColumn(name: "ActualGate", table: "Flights", newName: "Gate");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ScheduledDeparture", table: "Flights", type: "timestamp with time zone", nullable: true,
                oldClrType: typeof(DateTime), oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ScheduledArrival", table: "Flights", type: "timestamp with time zone", nullable: true,
                oldClrType: typeof(DateTime), oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "ArrivalAirportCode", table: "Flights", type: "character varying(10)", maxLength: 10, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "DelayReason", table: "Flights", type: "character varying(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "DepartureAirportCode", table: "Flights", type: "character varying(10)", maxLength: 10, nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "GuestId", table: "Flights", type: "uuid", nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.AddColumn<bool>(
                name: "IsArrival", table: "Flights", type: "boolean", nullable: false, defaultValue: false);

            migrationBuilder.CreateIndex(name: "IX_Flights_GuestId", table: "Flights", column: "GuestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_Guests_GuestId", table: "Flights", column: "GuestId",
                principalTable: "Guests", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        }
    }
}
