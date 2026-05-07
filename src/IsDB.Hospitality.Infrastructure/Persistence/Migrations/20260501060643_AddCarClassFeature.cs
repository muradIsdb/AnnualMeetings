using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarClassFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CarClassId",
                table: "Vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeservedCarClassId",
                table: "Guests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CarClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarClasses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CarClassId",
                table: "Vehicles",
                column: "CarClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_DeservedCarClassId",
                table: "Guests",
                column: "DeservedCarClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_CarClasses_DeservedCarClassId",
                table: "Guests",
                column: "DeservedCarClassId",
                principalTable: "CarClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_CarClasses_CarClassId",
                table: "Vehicles",
                column: "CarClassId",
                principalTable: "CarClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guests_CarClasses_DeservedCarClassId",
                table: "Guests");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_CarClasses_CarClassId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "CarClasses");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_CarClassId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Guests_DeservedCarClassId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "CarClassId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DeservedCarClassId",
                table: "Guests");
        }
    }
}
