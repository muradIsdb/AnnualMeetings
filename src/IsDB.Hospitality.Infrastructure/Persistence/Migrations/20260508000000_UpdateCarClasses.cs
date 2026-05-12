using System;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCarClasses : Migration
    {
        // Existing UAT car class IDs (seeded by DatabaseSeeder)
        private static readonly Guid LuxuryCarId  = new Guid("3edfb309-6db0-4837-b246-5b6b242c42ed");
        private static readonly Guid AmocCarId     = new Guid("76f802cd-7d5c-4a75-9ae8-fb88b2b3b368");
        private static readonly Guid StandardCarId = new Guid("775ed3a7-becc-4a00-a0c1-566acd7fc65d");

        // New IDs for the 3 additional classes
        private static readonly Guid ExecutiveLuxuryId = new Guid("a1b2c3d4-0001-0001-0001-000000000001");
        private static readonly Guid ExecutiveSuvId    = new Guid("a1b2c3d4-0002-0002-0002-000000000002");
        private static readonly Guid BoardDgClassId    = new Guid("a1b2c3d4-0003-0003-0003-000000000003");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = DateTime.UtcNow;

            // ── UPDATE existing 3 classes ──────────────────────────────────────────

            // 1. Luxury Car → VVIP Luxury (sort 1, purple #7C3AED — same color)
            migrationBuilder.Sql($@"
                UPDATE ""CarClasses""
                SET ""Name"" = 'VVIP Luxury',
                    ""Description"" = 'Reserved for VVIP guests — heads of state, ministers, and senior dignitaries',
                    ""Color"" = '#7C3AED',
                    ""SortOrder"" = 1,
                    ""UpdatedAt"" = '{now:yyyy-MM-dd HH:mm:ss.fff}+00'
                WHERE ""Id"" = '{LuxuryCarId}';
            ");

            // 2. AMOC Car → AMOC (sort 5, blue #0369A1 — same color)
            migrationBuilder.Sql($@"
                UPDATE ""CarClasses""
                SET ""Name"" = 'AMOC',
                    ""Description"" = 'AMOC-designated vehicles for the organizing committee',
                    ""Color"" = '#0369A1',
                    ""SortOrder"" = 5,
                    ""UpdatedAt"" = '{now:yyyy-MM-dd HH:mm:ss.fff}+00'
                WHERE ""Id"" = '{AmocCarId}';
            ");

            // 3. Standard Car → General Pool (sort 6, green #059669 — same color)
            migrationBuilder.Sql($@"
                UPDATE ""CarClasses""
                SET ""Name"" = 'General Pool',
                    ""Description"" = 'General pool vehicles for standard participants and staff',
                    ""Color"" = '#059669',
                    ""SortOrder"" = 6,
                    ""UpdatedAt"" = '{now:yyyy-MM-dd HH:mm:ss.fff}+00'
                WHERE ""Id"" = '{StandardCarId}';
            ");

            // ── INSERT 3 new classes ───────────────────────────────────────────────

            // 4. Executive Luxury (sort 2)
            migrationBuilder.Sql($@"
                INSERT INTO ""CarClasses"" (""Id"", ""Name"", ""Description"", ""Color"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (
                    '{ExecutiveLuxuryId}',
                    'Executive Luxury',
                    'Premium luxury sedans for senior executives and VIP guests',
                    '#B45309',
                    2,
                    '{now:yyyy-MM-dd HH:mm:ss.fff}+00',
                    '{now:yyyy-MM-dd HH:mm:ss.fff}+00'
                );
            ");

            // 5. Executive SUV (sort 3)
            migrationBuilder.Sql($@"
                INSERT INTO ""CarClasses"" (""Id"", ""Name"", ""Description"", ""Color"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (
                    '{ExecutiveSuvId}',
                    'Executive SUV',
                    'Executive-class SUVs for senior officials and delegations',
                    '#0E7490',
                    3,
                    '{now:yyyy-MM-dd HH:mm:ss.fff}+00',
                    '{now:yyyy-MM-dd HH:mm:ss.fff}+00'
                );
            ");

            // 6. Board & DG Class (sort 4)
            migrationBuilder.Sql($@"
                INSERT INTO ""CarClasses"" (""Id"", ""Name"", ""Description"", ""Color"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (
                    '{BoardDgClassId}',
                    'Board & DG Class',
                    'Dedicated vehicles for Board of Governors members and the Director General',
                    '#DC2626',
                    4,
                    '{now:yyyy-MM-dd HH:mm:ss.fff}+00',
                    '{now:yyyy-MM-dd HH:mm:ss.fff}+00'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var now = DateTime.UtcNow;

            // Remove the 3 new classes
            migrationBuilder.Sql($@"DELETE FROM ""CarClasses"" WHERE ""Id"" IN ('{ExecutiveLuxuryId}', '{ExecutiveSuvId}', '{BoardDgClassId}');");

            // Revert the 3 updated classes to their original names/values
            migrationBuilder.Sql($@"
                UPDATE ""CarClasses""
                SET ""Name"" = 'Luxury Car',
                    ""Description"" = 'High-end luxury vehicles for VIP and VVIP guests',
                    ""Color"" = '#7C3AED',
                    ""SortOrder"" = 1,
                    ""UpdatedAt"" = '{now:yyyy-MM-dd HH:mm:ss.fff}+00'
                WHERE ""Id"" = '{LuxuryCarId}';
            ");
            migrationBuilder.Sql($@"
                UPDATE ""CarClasses""
                SET ""Name"" = 'AMOC Car',
                    ""Description"" = 'AMOC-designated vehicles for organizing committee',
                    ""Color"" = '#0369A1',
                    ""SortOrder"" = 2,
                    ""UpdatedAt"" = '{now:yyyy-MM-dd HH:mm:ss.fff}+00'
                WHERE ""Id"" = '{AmocCarId}';
            ");
            migrationBuilder.Sql($@"
                UPDATE ""CarClasses""
                SET ""Name"" = 'Standard Car',
                    ""Description"" = 'Standard vehicles for general participants',
                    ""Color"" = '#059669',
                    ""SortOrder"" = 3,
                    ""UpdatedAt"" = '{now:yyyy-MM-dd HH:mm:ss.fff}+00'
                WHERE ""Id"" = '{StandardCarId}';
            ");
        }
    }
}
