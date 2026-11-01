using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardLinkTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkTarget",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinkTarget",
                table: "Cards");
        }
    }
}
