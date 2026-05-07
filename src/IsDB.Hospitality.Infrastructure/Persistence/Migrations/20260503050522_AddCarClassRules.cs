using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarClassRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                // SQLite: use TEXT for all types (SQLite is flexible)
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""CarClassRules"" (
    ""Id""                   TEXT NOT NULL PRIMARY KEY,
    ""RegistrationTypeName"" TEXT NOT NULL,
    ""CarClassId""           TEXT NOT NULL,
    ""Priority""             INTEGER NOT NULL DEFAULT 10,
    ""Notes""                TEXT,
    ""CreatedAt""            TEXT NOT NULL DEFAULT (datetime('now')),
    ""UpdatedAt""            TEXT NOT NULL DEFAULT (datetime('now')),
    CONSTRAINT ""FK_CarClassRules_CarClasses_CarClassId""
        FOREIGN KEY (""CarClassId"") REFERENCES ""CarClasses""(""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CarClassRules_RegistrationTypeName"" ON ""CarClassRules""(""RegistrationTypeName"");
CREATE INDEX IF NOT EXISTS ""IX_CarClassRules_CarClassId"" ON ""CarClassRules""(""CarClassId"");
                ");
            }
            else
            {
                // PostgreSQL: use uuid and timestamptz
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""CarClassRules"" (
    ""Id""                   uuid NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
    ""RegistrationTypeName"" text NOT NULL,
    ""CarClassId""           uuid NOT NULL,
    ""Priority""             integer NOT NULL DEFAULT 10,
    ""Notes""                text,
    ""CreatedAt""            timestamptz NOT NULL DEFAULT now(),
    ""UpdatedAt""            timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ""FK_CarClassRules_CarClasses_CarClassId""
        FOREIGN KEY (""CarClassId"") REFERENCES ""CarClasses""(""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CarClassRules_RegistrationTypeName"" ON ""CarClassRules""(""RegistrationTypeName"");
CREATE INDEX IF NOT EXISTS ""IX_CarClassRules_CarClassId"" ON ""CarClassRules""(""CarClassId"");
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""CarClassRules"";");
        }
    }
}
