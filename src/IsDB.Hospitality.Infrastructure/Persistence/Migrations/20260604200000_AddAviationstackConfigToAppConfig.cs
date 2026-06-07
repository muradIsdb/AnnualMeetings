using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAviationstackConfigToAppConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""AppConfigs""
                    ADD COLUMN IF NOT EXISTS ""AviationstackApiKey"" text NULL;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""AppConfigs""
                    ADD COLUMN IF NOT EXISTS ""AviationstackSyncIntervalMinutes"" integer NOT NULL DEFAULT 5;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""AppConfigs""
                    ADD COLUMN IF NOT EXISTS ""AviationstackTrackingWindowHours"" integer NOT NULL DEFAULT 12;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""AppConfigs"" DROP COLUMN IF EXISTS ""AviationstackApiKey"";");
            migrationBuilder.Sql(@"ALTER TABLE ""AppConfigs"" DROP COLUMN IF EXISTS ""AviationstackSyncIntervalMinutes"";");
            migrationBuilder.Sql(@"ALTER TABLE ""AppConfigs"" DROP COLUMN IF EXISTS ""AviationstackTrackingWindowHours"";");
        }
    }
}
