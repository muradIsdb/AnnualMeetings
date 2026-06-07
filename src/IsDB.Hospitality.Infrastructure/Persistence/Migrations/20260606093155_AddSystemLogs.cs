using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add LogRetentionDays to AppConfigs if it doesn't already exist
            // (idempotent — safe to run on both fresh and existing databases)
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'AppConfigs' AND column_name = 'LogRetentionDays'
                    ) THEN
                        ALTER TABLE ""AppConfigs"" ADD COLUMN ""LogRetentionDays"" integer NOT NULL DEFAULT 90;
                    END IF;
                END $$;
            ");

            // Create SystemLogs table (idempotent)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""SystemLogs"" (
                    ""Id""              uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                    ""Severity""        integer     NOT NULL DEFAULT 0,
                    ""Category""        text        NOT NULL DEFAULT '',
                    ""Module""          text        NOT NULL DEFAULT '',
                    ""Title""           text        NOT NULL DEFAULT '',
                    ""Detail""          text        NULL,
                    ""RequestPath""     text        NULL,
                    ""StaffUserId""     uuid        NULL,
                    ""StaffUserName""   text        NULL,
                    ""CorrelationId""   text        NULL,
                    ""OccurredAt""      timestamptz NOT NULL DEFAULT now()
                );
                CREATE INDEX IF NOT EXISTS ""IX_SystemLogs_OccurredAt"" ON ""SystemLogs""(""OccurredAt"" DESC);
                CREATE INDEX IF NOT EXISTS ""IX_SystemLogs_Severity"" ON ""SystemLogs""(""Severity"");
                CREATE INDEX IF NOT EXISTS ""IX_SystemLogs_Module"" ON ""SystemLogs""(""Module"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""SystemLogs"";");
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'AppConfigs' AND column_name = 'LogRetentionDays'
                    ) THEN
                        ALTER TABLE ""AppConfigs"" DROP COLUMN ""LogRetentionDays"";
                    END IF;
                END $$;
            ");
        }
    }
}
