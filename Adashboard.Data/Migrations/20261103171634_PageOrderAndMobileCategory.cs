using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class PageOrderAndMobileCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Pages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MobileWidth",
                table: "Categories",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "MobileWidth",
                table: "Categories");
        }
    }
}
