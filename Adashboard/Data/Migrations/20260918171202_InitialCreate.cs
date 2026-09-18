using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DashboardLayouts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ThemeMode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardLayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DashboardCategories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DashboardLayoutId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayWidth = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardCategories_DashboardLayouts_DashboardLayoutId",
                        column: x => x.DashboardLayoutId,
                        principalTable: "DashboardLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DashboardCards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CategoryId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IconClass = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IconColor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardCards_DashboardCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "DashboardCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardCards_CategoryId",
                table: "DashboardCards",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardCategories_DashboardLayoutId",
                table: "DashboardCategories",
                column: "DashboardLayoutId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DashboardCards");

            migrationBuilder.DropTable(
                name: "DashboardCategories");

            migrationBuilder.DropTable(
                name: "DashboardLayouts");
        }
    }
}
