using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLogoBase64ToAppConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventLogoBase64",
                table: "AppConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventLogoMimeType",
                table: "AppConfigs",
                type: "varchar(50)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventLogoBase64",
                table: "AppConfigs");

            migrationBuilder.DropColumn(
                name: "EventLogoMimeType",
                table: "AppConfigs");
        }
    }
}
