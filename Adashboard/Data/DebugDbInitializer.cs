using Adashboard.Models.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Data;

/// <summary>
/// Инициализирует базу данных в режиме разработки.
/// </summary>
public static class DebugDbInitializer
{
    /// <summary>
    /// Применяет миграции и добавляет стартовые данные, если раскладка ещё не создавалась.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Adashboard.Data.DebugDbInitializer");

        logger.LogInformation("Запущена инициализация базы данных dashboard (Development).");

        await DashboardDatabaseBootstrap.ApplyMigrationsAsync(dbContext, logger, cancellationToken);

        // Наличие раскладки служит признаком, что первичное наполнение уже выполнялось:
        // ручное удаление всех категорий не должно возвращать стартовые карточки.
        var hasLayout = await dbContext.Layouts.AnyAsync(cancellationToken);
        if (hasLayout)
        {
            logger.LogInformation("Стартовые данные уже присутствуют в базе данных. Инициализация завершена.");
            return;
        }

        var layout = CreateDefaultLayout();
        dbContext.Layouts.Add(layout);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Добавлена стартовая раскладка dashboard. Категорий: {CategoryCount}.",
            layout.Categories.Count);
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
                        CreateCard(1002, 101, "Uptime Kuma", "https://example.local/uptime", "fa-solid fa-heart-pulse", "#22c55e", false, 1),
                        CreateCard(1003, 101, "Portainer", "https://example.local/portainer", "fa-brands fa-docker", "#0ea5e9", true, 2),
                        CreateCard(1004, 101, "Ansible", "https://example.local/ansible", "fa-solid fa-terminal", "#8b5cf6", false, 3)
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
                        CreateCard(2001, 102, "Jellyfin", "https://example.local/jellyfin", "fa-solid fa-film", "#a78bfa", false, 0),
                        CreateCard(2002, 102, "Sonarr", "https://example.local/sonarr", "fa-solid fa-satellite-dish", "#fb7185", true, 1),
                        CreateCard(2003, 102, "Radarr", "https://example.local/radarr", "fa-solid fa-clapperboard", "#facc15", true, 2),
                        CreateCard(2004, 102, "Bazarr", "https://example.local/bazarr", "fa-solid fa-closed-captioning", "#60a5fa", false, 3),
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
                        CreateCard(3002, 103, "Prometheus", "https://example.local/prometheus", "fa-solid fa-chart-area", "#22d3ee", false, 1),
                        CreateCard(3003, 103, "Adminer", "https://example.local/adminer", "fa-solid fa-database", "#f43f5e", true, 2)
                    ]
                }
            ]
        };

        return layout;
    }

    /// <summary>
    /// Создаёт карточку с заданным идентификатором и позицией.
    /// </summary>
    private static DashboardCard CreateCard(int id, int categoryId, string title, string url, string iconClass, string backgroundColor, bool isWide, int order) =>
        new()
        {
            Id = id,
            CategoryId = categoryId,
            Title = title,
            Url = url,
            IconClass = iconClass,
            BackgroundColor = backgroundColor,
            IsWide = isWide,
            Position = new CardPosition { Order = order }
        };
}
