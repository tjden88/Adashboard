using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardImagePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "DashboardCards",
                type: "TEXT",
                maxLength: 260,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "DashboardCards");
        }
    }
}
