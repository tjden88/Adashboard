using Adashboard.Models.Dashboard;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Adashboard.Data;

/// <summary>
/// Инициализирует базу данных в режиме разработки.
/// </summary>
public static class DebugDbInitializer
{
    /// <summary>
    /// Применяет миграции и добавляет стартовые данные, если таблицы пусты.
    /// </summary>
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

    /// <summary>
    /// Добавляет запись о первой миграции для базы, созданной ранее через EnsureCreated.
    /// </summary>
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

    /// <summary>
    /// Проверяет существование таблицы в SQLite.
    /// </summary>
    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.Add(CreateParameter(command, "$tableName", tableName));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    /// <summary>
    /// Проверяет наличие записи о миграции в таблице истории.
    /// </summary>
    private static async Task<bool> MigrationExistsAsync(DbConnection connection, string migrationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = $migrationId;";
        command.Parameters.Add(CreateParameter(command, "$migrationId", migrationId));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    /// <summary>
    /// Создаёт параметр команды для SQL-запроса.
    /// </summary>
    private static DbParameter CreateParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }

    /// <summary>
    /// Формирует стартовую раскладку dashboard.
    /// </summary>
    private static DashboardLayout CreateDefaultLayout()
    {
        var layout = new DashboardLayout
        {
            Id = 1,
            ThemeMode = "auto",
            Categories =
            [
                new DashboardCategory
                {
                    Id = 101,
                    DashboardLayoutId = 1,
                    Title = "Networking & Management",
                    Position = new CategoryPosition { Order = 0, Width = 6 },
                    Cards =
                    [
                        CreateCard(1001, 101, "Nginx Proxy Manager", "https://example.local/nginx", "fa-solid fa-network-wired", "#f43f5e", true, 0),
                        CreateCard(1002, 101, "Uptime Kuma", "https://example.local/uptime", "fa-solid fa-heart-pulse", "#22c55e", true, 1),
                        CreateCard(1003, 101, "Portainer", "https://example.local/portainer", "fa-brands fa-docker", "#0ea5e9", true, 2),
                        CreateCard(1004, 101, "Ansible", "https://example.local/ansible", "fa-solid fa-terminal", "#8b5cf6", true, 3)
                    ]
                },
                new DashboardCategory
                {
                    Id = 102,
                    DashboardLayoutId = 1,
                    Title = "Mediaserver",
                    Position = new CategoryPosition { Order = 1, Width = 8 },
                    Cards =
                    [
                        CreateCard(2001, 102, "Jellyfin", "https://example.local/jellyfin", "fa-solid fa-film", "#a78bfa", true, 0),
                        CreateCard(2002, 102, "Sonarr", "https://example.local/sonarr", "fa-solid fa-satellite-dish", "#fb7185", true, 1),
                        CreateCard(2003, 102, "Radarr", "https://example.local/radarr", "fa-solid fa-clapperboard", "#facc15", true, 2),
                        CreateCard(2004, 102, "Bazarr", "https://example.local/bazarr", "fa-solid fa-closed-captioning", "#60a5fa", true, 3),
                        CreateCard(2005, 102, "Lidarr", "https://example.local/lidarr", "fa-solid fa-compact-disc", "#34d399", true, 4),
                        CreateCard(2006, 102, "SABnzbd", "https://example.local/sab", "fa-solid fa-download", "#22d3ee", true, 5)
                    ]
                },
                new DashboardCategory
                {
                    Id = 103,
                    DashboardLayoutId = 1,
                    Title = "Services",
                    Position = new CategoryPosition { Order = 2, Width = 4 },
                    Cards =
                    [
                        CreateCard(3001, 103, "Grafana", "https://example.local/grafana", "fa-solid fa-chart-line", "#f97316", true, 0),
                        CreateCard(3002, 103, "Prometheus", "https://example.local/prometheus", "fa-solid fa-chart-area", "#22d3ee", true, 1),
                        CreateCard(3003, 103, "Adminer", "https://example.local/adminer", "fa-solid fa-database", "#f43f5e", false, 2)
                    ]
                }
            ]
        };

        return layout;
    }

    /// <summary>
    /// Создаёт карточку с заданным идентификатором и позицией.
    /// </summary>
    private static DashboardCard CreateCard(int id, int categoryId, string title, string url, string iconClass, string iconColor, bool isOnline, int order) =>
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
