using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FlightDataLayerSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ChangedAt",
                table: "TravelBookings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChangedSinceLastView",
                table: "TravelBookings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreviousFlightNumber",
                table: "TravelBookings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TravelBookingHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TravelBookingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PreviousFlightNumber = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousAirlineName = table.Column<string>(type: "TEXT", nullable: true),
                    PreviousScheduledArrival = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousScheduledDeparture = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousDeparturePort = table.Column<string>(type: "TEXT", nullable: true),
                    PreviousArrivalPort = table.Column<string>(type: "TEXT", nullable: true),
                    PreviousSeatClass = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelBookingHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelBookingHistories_TravelBookings_TravelBookingId",
                        column: x => x.TravelBookingId,
                        principalTable: "TravelBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookingHistories_TravelBookingId",
                table: "TravelBookingHistories",
                column: "TravelBookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TravelBookingHistories");

            migrationBuilder.DropColumn(
                name: "ChangedAt",
                table: "TravelBookings");

            migrationBuilder.DropColumn(
                name: "ChangedSinceLastView",
                table: "TravelBookings");

            migrationBuilder.DropColumn(
                name: "PreviousFlightNumber",
                table: "TravelBookings");
        }
    }
}
