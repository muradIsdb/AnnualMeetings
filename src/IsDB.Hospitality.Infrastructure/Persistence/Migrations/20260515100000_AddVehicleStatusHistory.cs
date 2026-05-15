using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL so this migration is idempotent on PostgreSQL.
            // The pre-creation block in Program.cs already creates the table on existing Railway DBs.
            // This migration handles fresh EF Core-managed databases.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""VehicleStatusHistories"" (
                    ""Id""                 uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                    ""VehicleId""          uuid        NOT NULL REFERENCES ""Vehicles""(""Id"") ON DELETE CASCADE,
                    ""OldStatus""          integer     NOT NULL,
                    ""NewStatus""          integer     NOT NULL,
                    ""ChangedByStaffId""   uuid        NULL REFERENCES ""StaffUsers""(""Id"") ON DELETE SET NULL,
                    ""ChangedByName""      text        NULL,
                    ""ChangedByRole""      integer     NULL,
                    ""Notes""              text        NULL,
                    ""CreatedAt""          timestamptz NOT NULL DEFAULT now(),
                    ""UpdatedAt""          timestamptz NOT NULL DEFAULT now()
                );
                CREATE INDEX IF NOT EXISTS ""IX_VehicleStatusHistories_VehicleId"" ON ""VehicleStatusHistories""(""VehicleId"");
                CREATE INDEX IF NOT EXISTS ""IX_VehicleStatusHistories_ChangedByStaffId"" ON ""VehicleStatusHistories""(""ChangedByStaffId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""VehicleStatusHistories"";");
        }
    }
}
