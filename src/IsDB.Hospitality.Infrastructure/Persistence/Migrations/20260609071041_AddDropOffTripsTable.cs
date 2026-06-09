using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDropOffTripsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DropOffTrips table and ShortName column are created by Program.cs startup
            // using raw SQL with proper PostgreSQL types (uuid, timestamptz) and
            // CREATE TABLE IF NOT EXISTS / IF NOT EXISTS guards.
            // This migration is intentionally a no-op to avoid SQLite TEXT vs PostgreSQL
            // UUID type mismatches on foreign key constraints.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — table managed by Program.cs.
        }
    }
}
