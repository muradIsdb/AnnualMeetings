using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DepartureRequests_Guests_GuestId",
                table: "DepartureRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_DepartureRequests_StaffUsers_ProcessedByStaffId",
                table: "DepartureRequests");

            migrationBuilder.DropIndex(
                name: "IX_DepartureRequests_GuestId",
                table: "DepartureRequests");

            migrationBuilder.DropIndex(
                name: "IX_DepartureRequests_ProcessedByStaffId",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "DestinationAirport",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "FlightNumber",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "GuestId",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "HotelName",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "ProcessedByStaffId",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "SpecialRequirements",
                table: "DepartureRequests");

            migrationBuilder.RenameColumn(
                name: "RequestedPickupTime",
                table: "DepartureRequests",
                newName: "SubmittedAt");

            migrationBuilder.RenameColumn(
                name: "IsProcessed",
                table: "DepartureRequests",
                newName: "DisclaimerAccepted");

            migrationBuilder.RenameColumn(
                name: "GuestName",
                table: "DepartureRequests",
                newName: "FullName");

            migrationBuilder.AddColumn<int>(
                name: "InboundStatus",
                table: "Guests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OutboundStatus",
                table: "Guests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceivedByEmbassyTeam",
                table: "Guests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "RoomNumber",
                table: "DepartureRequests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "DepartureRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "HotelOptionId",
                table: "DepartureRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ManageToken",
                table: "DepartureRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PickupDayOptionId",
                table: "DepartureRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PickupHourOptionId",
                table: "DepartureRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Alerts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TargetRole",
                table: "Alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventTitle = table.Column<string>(type: "text", nullable: false),
                    MinimumLeadTimeHours = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigs", x => x.Id);
                });

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
                    IsSystemGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsRolledBack = table.Column<bool>(type: "boolean", nullable: false),
                    RolledBackByHistoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    TargetRoles = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedByStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_StaffUsers_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationReads",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReads", x => new { x.NotificationId, x.StaffUserId });
                    table.ForeignKey(
                        name: "FK_NotificationReads_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationReads_StaffUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppConfigs",
                columns: new[] { "Id", "EventTitle", "MinimumLeadTimeHours", "UpdatedAt" },
                values: new object[] { 1, "IsDB Annual Meetings 2025", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_Email",
                table: "DepartureRequests",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_HotelOptionId",
                table: "DepartureRequests",
                column: "HotelOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_ManageToken",
                table: "DepartureRequests",
                column: "ManageToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_PickupDayOptionId",
                table: "DepartureRequests",
                column: "PickupDayOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_PickupHourOptionId",
                table: "DepartureRequests",
                column: "PickupHourOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestStatusHistories_ChangedByStaffId",
                table: "GuestStatusHistories",
                column: "ChangedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestStatusHistories_GuestId",
                table: "GuestStatusHistories",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_StaffUserId",
                table: "NotificationReads",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedByStaffId",
                table: "Notifications",
                column: "CreatedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureRequests_HotelOptions_HotelOptionId",
                table: "DepartureRequests",
                column: "HotelOptionId",
                principalTable: "HotelOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DepartureRequests_HotelOptions_HotelOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_DepartureRequests_PickupDayOptions_PickupDayOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_DepartureRequests_PickupHourOptions_PickupHourOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropTable(
                name: "AppConfigs");

            migrationBuilder.DropTable(
                name: "GuestStatusHistories");

            migrationBuilder.DropTable(
                name: "NotificationReads");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_DepartureRequests_Email",
                table: "DepartureRequests");

            migrationBuilder.DropIndex(
                name: "IX_DepartureRequests_HotelOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropIndex(
                name: "IX_DepartureRequests_ManageToken",
                table: "DepartureRequests");

            migrationBuilder.DropIndex(
                name: "IX_DepartureRequests_PickupDayOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropIndex(
                name: "IX_DepartureRequests_PickupHourOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "InboundStatus",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "OutboundStatus",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "ReceivedByEmbassyTeam",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "HotelOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "ManageToken",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "PickupDayOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "PickupHourOptionId",
                table: "DepartureRequests");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "TargetRole",
                table: "Alerts");

            migrationBuilder.RenameColumn(
                name: "SubmittedAt",
                table: "DepartureRequests",
                newName: "RequestedPickupTime");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "DepartureRequests",
                newName: "GuestName");

            migrationBuilder.RenameColumn(
                name: "DisclaimerAccepted",
                table: "DepartureRequests",
                newName: "IsProcessed");

            migrationBuilder.AlterColumn<string>(
                name: "RoomNumber",
                table: "DepartureRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "DestinationAirport",
                table: "DepartureRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FlightNumber",
                table: "DepartureRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "DepartureRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GuestId",
                table: "DepartureRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "DepartureRequests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HotelName",
                table: "DepartureRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "DepartureRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessedByStaffId",
                table: "DepartureRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialRequirements",
                table: "DepartureRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_GuestId",
                table: "DepartureRequests",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_ProcessedByStaffId",
                table: "DepartureRequests",
                column: "ProcessedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureRequests_Guests_GuestId",
                table: "DepartureRequests",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DepartureRequests_StaffUsers_ProcessedByStaffId",
                table: "DepartureRequests",
                column: "ProcessedByStaffId",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
