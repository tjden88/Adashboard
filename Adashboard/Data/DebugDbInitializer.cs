using Adashboard.Models.Dashboard;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Adashboard.Data;

public static class DebugDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();

        await EnsureMigrationHistoryForLegacyDatabaseAsync(dbContext, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);

        var hasData = await dbContext.Layouts.AnyAsync(cancellationToken);
        if (hasData)
        {
            return;
        }

        var layout = CreateDefaultLayout();
        dbContext.Layouts.Add(layout);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureMigrationHistoryForLegacyDatabaseAsync(DashboardDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            var hasHistoryTable = await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken);
            var hasLegacyTables = await TableExistsAsync(connection, "DashboardLayouts", cancellationToken);
            if (!hasLegacyTables)
            {
                return;
            }

            var firstMigration = dbContext.Database.GetMigrations().OrderBy(x => x).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstMigration))
            {
                return;
            }

            if (hasHistoryTable)
            {
                var hasInitialMigrationRow = await MigrationExistsAsync(connection, firstMigration, cancellationToken);
                if (hasInitialMigrationRow)
                {
                    return;
                }
            }
            else
            {
                await using var createHistoryCommand = connection.CreateCommand();
                createHistoryCommand.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    );
                    """;
                await createHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var insertHistoryCommand = connection.CreateCommand();
            insertHistoryCommand.CommandText =
                "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($migrationId, $productVersion);";
            insertHistoryCommand.Parameters.Add(CreateParameter(insertHistoryCommand, "$migrationId", firstMigration));
            insertHistoryCommand.Parameters.Add(CreateParameter(insertHistoryCommand, "$productVersion", "10.0.12"));
            await insertHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.Add(CreateParameter(command, "$tableName", tableName));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<bool> MigrationExistsAsync(DbConnection connection, string migrationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = $migrationId;";
        command.Parameters.Add(CreateParameter(command, "$migrationId", migrationId));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static DbParameter CreateParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }

    private static DashboardLayout CreateDefaultLayout()
    {
        var layout = new DashboardLayout
        {
            Id = "main",
            ThemeMode = "auto",
            Categories =
            [
                new DashboardCategory
                {
                    Id = "cat-network",
                    DashboardLayoutId = "main",
                    Title = "Networking & Management",
                    Position = new CategoryPosition { Order = 0, Width = 6 },
                    Cards =
                    [
                        CreateCard("card-nginx", "cat-network", "Nginx Proxy Manager", "https://example.local/nginx", "fa-solid fa-network-wired", "#f43f5e", true, 0),
                        CreateCard("card-uptime", "cat-network", "Uptime Kuma", "https://example.local/uptime", "fa-solid fa-heart-pulse", "#22c55e", true, 1),
                        CreateCard("card-portainer", "cat-network", "Portainer", "https://example.local/portainer", "fa-brands fa-docker", "#0ea5e9", true, 2),
                        CreateCard("card-ansible", "cat-network", "Ansible", "https://example.local/ansible", "fa-solid fa-terminal", "#8b5cf6", true, 3)
                    ]
                },
                new DashboardCategory
                {
                    Id = "cat-mediaserver",
                    DashboardLayoutId = "main",
                    Title = "Mediaserver",
                    Position = new CategoryPosition { Order = 1, Width = 8 },
                    Cards =
                    [
                        CreateCard("card-jellyfin", "cat-mediaserver", "Jellyfin", "https://example.local/jellyfin", "fa-solid fa-film", "#a78bfa", true, 0),
                        CreateCard("card-sonarr", "cat-mediaserver", "Sonarr", "https://example.local/sonarr", "fa-solid fa-satellite-dish", "#fb7185", true, 1),
                        CreateCard("card-radarr", "cat-mediaserver", "Radarr", "https://example.local/radarr", "fa-solid fa-clapperboard", "#facc15", true, 2),
                        CreateCard("card-bazarr", "cat-mediaserver", "Bazarr", "https://example.local/bazarr", "fa-solid fa-closed-captioning", "#60a5fa", true, 3),
                        CreateCard("card-lidarr", "cat-mediaserver", "Lidarr", "https://example.local/lidarr", "fa-solid fa-compact-disc", "#34d399", true, 4),
                        CreateCard("card-sab", "cat-mediaserver", "SABnzbd", "https://example.local/sab", "fa-solid fa-download", "#22d3ee", true, 5)
                    ]
                },
                new DashboardCategory
                {
                    Id = "cat-services",
                    DashboardLayoutId = "main",
                    Title = "Services",
                    Position = new CategoryPosition { Order = 2, Width = 4 },
                    Cards =
                    [
                        CreateCard("card-grafana", "cat-services", "Grafana", "https://example.local/grafana", "fa-solid fa-chart-line", "#f97316", true, 0),
                        CreateCard("card-prom", "cat-services", "Prometheus", "https://example.local/prometheus", "fa-solid fa-chart-area", "#22d3ee", true, 1),
                        CreateCard("card-admin", "cat-services", "Adminer", "https://example.local/adminer", "fa-solid fa-database", "#f43f5e", false, 2)
                    ]
                }
            ]
        };

        return layout;
    }

    private static DashboardCard CreateCard(string id, string categoryId, string title, string url, string iconClass, string iconColor, bool isOnline, int order) =>
        new()
        {
            Id = id,
            CategoryId = categoryId,
            Title = title,
            Url = url,
            IconClass = iconClass,
            IconColor = iconColor,
            IsOnline = isOnline,
            Position = new CardPosition { Order = order }
        };
}
