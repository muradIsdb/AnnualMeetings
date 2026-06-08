using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingTagsToGuest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add InvitedToOpeningCeremony column (idempotent)
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Guests' AND column_name = 'InvitedToOpeningCeremony'
                    ) THEN
                        ALTER TABLE ""Guests"" ADD COLUMN ""InvitedToOpeningCeremony"" boolean NULL;
                    END IF;
                END $$;
            ");
            // Add OldHotel column (idempotent)
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Guests' AND column_name = 'OldHotel'
                    ) THEN
                        ALTER TABLE ""Guests"" ADD COLUMN ""OldHotel"" text NULL;
                    END IF;
                END $$;
            ");
            // Register migration in history (idempotent)
            migrationBuilder.Sql(@"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ('20260608110000_AddMarketingTagsToGuest', '8.0.11')
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
                        WHERE table_name = 'Guests' AND column_name = 'InvitedToOpeningCeremony'
                    ) THEN
                        ALTER TABLE ""Guests"" DROP COLUMN ""InvitedToOpeningCeremony"";
                    END IF;
                END $$;
            ");
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Guests' AND column_name = 'OldHotel'
                    ) THEN
                        ALTER TABLE ""Guests"" DROP COLUMN ""OldHotel"";
                    END IF;
                END $$;
            ");
        }
    }
}
