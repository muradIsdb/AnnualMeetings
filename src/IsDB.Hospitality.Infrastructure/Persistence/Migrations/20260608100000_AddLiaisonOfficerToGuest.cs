using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLiaisonOfficerToGuest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add LiaisonOfficer column to Guests table (idempotent — safe to run on both fresh and existing databases)
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Guests' AND column_name = 'LiaisonOfficer'
                    ) THEN
                        ALTER TABLE ""Guests"" ADD COLUMN ""LiaisonOfficer"" boolean NULL;
                    END IF;
                END $$;
            ");
            // Register migration in history (idempotent)
            migrationBuilder.Sql(@"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ('20260608100000_AddLiaisonOfficerToGuest', '8.0.11')
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Guests' AND column_name = 'LiaisonOfficer'
                    ) THEN
                        ALTER TABLE ""Guests"" DROP COLUMN ""LiaisonOfficer"";
                    END IF;
                END $$;
            ");
        }
    }
}
