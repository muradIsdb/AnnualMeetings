using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestRegistrationTypeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistrationTypeId",
                table: "Guests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationTypeName",
                table: "Guests",
                type: "text",
                nullable: true);

            // HotelOptions, PickupDayOptions, PickupHourOptions may already exist
            // (created by the AddSettingsOptions migration that was added later in the chain).
            // Use raw SQL with IF NOT EXISTS to make this migration idempotent on fresh databases.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""HotelOptions"" (
                    ""Id""           TEXT    NOT NULL PRIMARY KEY,
                    ""Name""         TEXT    NOT NULL,
                    ""IsActive""     INTEGER NOT NULL DEFAULT 1,
                    ""DisplayOrder"" INTEGER NOT NULL DEFAULT 0,
                    ""CreatedAt""    TEXT    NOT NULL,
                    ""UpdatedAt""    TEXT    NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ""PickupDayOptions"" (
                    ""Id""           TEXT    NOT NULL PRIMARY KEY,
                    ""Label""        TEXT    NOT NULL,
                    ""Value""        TEXT    NOT NULL DEFAULT '',
                    ""IsActive""     INTEGER NOT NULL DEFAULT 1,
                    ""DisplayOrder"" INTEGER NOT NULL DEFAULT 0,
                    ""CreatedAt""    TEXT    NOT NULL,
                    ""UpdatedAt""    TEXT    NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ""PickupHourOptions"" (
                    ""Id""           TEXT    NOT NULL PRIMARY KEY,
                    ""Label""        TEXT    NOT NULL,
                    ""Value""        TEXT    NOT NULL DEFAULT '',
                    ""IsActive""     INTEGER NOT NULL DEFAULT 1,
                    ""DisplayOrder"" INTEGER NOT NULL DEFAULT 0,
                    ""CreatedAt""    TEXT    NOT NULL,
                    ""UpdatedAt""    TEXT    NOT NULL
                );
            ");

            migrationBuilder.CreateTable(
                name: "RegistrationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsSelectedForSync = table.Column<bool>(type: "boolean", nullable: false),
                    IsFromEventsAir = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationTypes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HotelOptions");

            migrationBuilder.DropTable(
                name: "PickupDayOptions");

            migrationBuilder.DropTable(
                name: "PickupHourOptions");

            migrationBuilder.DropTable(
                name: "RegistrationTypes");

            migrationBuilder.DropColumn(
                name: "RegistrationTypeId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "RegistrationTypeName",
                table: "Guests");
        }
    }
}
