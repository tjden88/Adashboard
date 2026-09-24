using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class CardStatusModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatusMode",
                table: "DashboardCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Переносим прежний флаг показа статуса: включённый статус становится режимом
            // «Сервис отвечает» (1), выключенный — режимом «Не проверять» (0).
            migrationBuilder.Sql(
                "UPDATE \"DashboardCards\" SET \"StatusMode\" = CASE WHEN \"ShowStatus\" = 1 THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "ShowStatus",
                table: "DashboardCards");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "DashboardCards");

            migrationBuilder.AddColumn<int>(
                name: "HealthCheckIntervalSeconds",
                table: "DashboardLayouts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HealthCheckIntervalSeconds",
                table: "DashboardLayouts");

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "DashboardCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowStatus",
                table: "DashboardCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                "UPDATE \"DashboardCards\" SET \"ShowStatus\" = CASE WHEN \"StatusMode\" <> 0 THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "StatusMode",
                table: "DashboardCards");
        }
    }
}
