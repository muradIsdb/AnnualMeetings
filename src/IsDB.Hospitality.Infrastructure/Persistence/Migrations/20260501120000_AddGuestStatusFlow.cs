using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestStatusFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add InboundStatus column to Guests (integer, default 0 = ArrivalScheduled)
            migrationBuilder.AddColumn<int>(
                name: "InboundStatus",
                table: "Guests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Add ReceivedByEmbassyTeam flag to Guests (boolean, default false)
            migrationBuilder.AddColumn<bool>(
                name: "ReceivedByEmbassyTeam",
                table: "Guests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Add OutboundStatus column to Guests (nullable integer)
            migrationBuilder.AddColumn<int>(
                name: "OutboundStatus",
                table: "Guests",
                type: "integer",
                nullable: true);

            // Create GuestStatusHistories table
            migrationBuilder.CreateTable(
                name: "GuestStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Track = table.Column<int>(type: "integer", nullable: false),
                    StatusValue = table.Column<int>(type: "integer", nullable: false),
                    StatusLabel = table.Column<string>(type: "text", nullable: false),
                    ChangedByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedByName = table.Column<string>(type: "text", nullable: true),
                    ChangedByRole = table.Column<int>(type: "integer", nullable: true),
                    IsSystemGenerated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsRolledBack = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RolledBackByHistoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestStatusHistories_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuestStatusHistories_StaffUsers_ChangedByStaffId",
                        column: x => x.ChangedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Add TargetRole and IsRead to Alerts table
            migrationBuilder.AddColumn<int>(
                name: "TargetRole",
                table: "Alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Alerts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_GuestStatusHistories_GuestId",
                table: "GuestStatusHistories",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestStatusHistories_ChangedByStaffId",
                table: "GuestStatusHistories",
                column: "ChangedByStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GuestStatusHistories");

            migrationBuilder.DropColumn(name: "InboundStatus", table: "Guests");
            migrationBuilder.DropColumn(name: "ReceivedByEmbassyTeam", table: "Guests");
            migrationBuilder.DropColumn(name: "OutboundStatus", table: "Guests");
        }
    }
}
