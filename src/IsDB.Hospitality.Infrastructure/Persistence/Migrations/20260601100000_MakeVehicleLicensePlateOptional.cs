using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeVehicleLicensePlateOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL so this migration is idempotent on PostgreSQL.
            // 1. Drop the existing unique index on LicensePlate (not null-safe)
            // 2. Alter the column to allow NULLs
            // 3. Re-create a partial unique index that only enforces uniqueness on non-NULL values
            migrationBuilder.Sql(@"
                -- Drop old unique index
                DROP INDEX IF EXISTS ""IX_Vehicles_LicensePlate"";

                -- Make LicensePlate nullable
                ALTER TABLE ""Vehicles""
                    ALTER COLUMN ""LicensePlate"" DROP NOT NULL;

                -- Re-create unique index that allows multiple NULLs (partial index)
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Vehicles_LicensePlate""
                    ON ""Vehicles""(""LicensePlate"")
                    WHERE ""LicensePlate"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Revert: drop partial index
                DROP INDEX IF EXISTS ""IX_Vehicles_LicensePlate"";

                -- Set empty plates to a placeholder before making NOT NULL again
                UPDATE ""Vehicles"" SET ""LicensePlate"" = 'UNKNOWN-' || gen_random_uuid()::text
                    WHERE ""LicensePlate"" IS NULL;

                -- Restore NOT NULL constraint
                ALTER TABLE ""Vehicles""
                    ALTER COLUMN ""LicensePlate"" SET NOT NULL;

                -- Restore original unique index
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Vehicles_LicensePlate""
                    ON ""Vehicles""(""LicensePlate"");
            ");
        }
    }
}
