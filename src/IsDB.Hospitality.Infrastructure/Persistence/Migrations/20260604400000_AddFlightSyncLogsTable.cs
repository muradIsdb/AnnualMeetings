using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFlightSyncLogsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "FlightSyncLogs" (
                "Id"                   uuid         NOT NULL DEFAULT gen_random_uuid(),
                "SyncedAt"             timestamptz  NOT NULL DEFAULT now(),
                "TriggerSource"        text         NOT NULL DEFAULT 'Scheduled',
                "Status"               text         NOT NULL DEFAULT 'Success',
                "FlightsInWindow"      integer      NOT NULL DEFAULT 0,
                "FlightsQueried"       integer      NOT NULL DEFAULT 0,
                "FlightsUpdated"       integer      NOT NULL DEFAULT 0,
                "DurationMs"           integer      NOT NULL DEFAULT 0,
                "Message"              text         NULL,
                "InitiatedByStaffName" text         NULL,
                "CreatedAt"            timestamptz  NOT NULL DEFAULT now(),
                "UpdatedAt"            timestamptz  NOT NULL DEFAULT now(),
                CONSTRAINT "PK_FlightSyncLogs" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_FlightSyncLogs_SyncedAt"
                ON "FlightSyncLogs" ("SyncedAt" DESC);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "FlightSyncLogs";""");
    }
}
