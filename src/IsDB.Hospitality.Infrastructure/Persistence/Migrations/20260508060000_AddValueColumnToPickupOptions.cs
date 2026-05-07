using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddValueColumnToPickupOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Value column to PickupDayOptions if it doesn't exist
            migrationBuilder.Sql(@"
                ALTER TABLE ""PickupDayOptions""
                ADD COLUMN IF NOT EXISTS ""Value"" TEXT NOT NULL DEFAULT '';
            ");

            // Add Value column to PickupHourOptions if it doesn't exist
            migrationBuilder.Sql(@"
                ALTER TABLE ""PickupHourOptions""
                ADD COLUMN IF NOT EXISTS ""Value"" TEXT NOT NULL DEFAULT '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Value", table: "PickupDayOptions");
            migrationBuilder.DropColumn(name: "Value", table: "PickupHourOptions");
        }
    }
}
