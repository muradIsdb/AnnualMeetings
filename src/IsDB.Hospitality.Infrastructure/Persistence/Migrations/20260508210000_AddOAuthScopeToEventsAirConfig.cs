using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthScopeToEventsAirConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use idempotent raw SQL so this migration is safe to run even if the column
            // was already added manually (e.g. by the legacy-DB pre-creation block in Program.cs).
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'EventsAirConfigs' AND column_name = 'OAuthScope'
                    ) THEN
                        ALTER TABLE ""EventsAirConfigs"" ADD COLUMN ""OAuthScope"" text NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OAuthScope",
                table: "EventsAirConfigs");
        }
    }
}
