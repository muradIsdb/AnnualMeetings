using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAviationstackDateGuardDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""AppConfigs""
                    ADD COLUMN IF NOT EXISTS ""AviationstackDateGuardDays"" integer NOT NULL DEFAULT 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""AppConfigs"" DROP COLUMN IF EXISTS ""AviationstackDateGuardDays"";");
        }
    }
}
