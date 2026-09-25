using Adashboard.Models.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Data;

/// <summary>
/// Инициализирует базу данных в рабочем режиме: применяет миграции и один раз добавляет стартовое наполнение.
/// </summary>
public static class ProductionDbInitializer
{
    /// <summary>
    /// Применяет миграции и добавляет стартовые категории со ссылками, если раскладка ещё не создавалась.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Adashboard.Data.ProductionDbInitializer");

        logger.LogInformation("Запущена инициализация базы данных dashboard (Production).");

        await DashboardDatabaseBootstrap.ApplyMigrationsAsync(dbContext, logger, cancellationToken);

        // Наличие раскладки служит признаком, что первичное наполнение уже выполнялось.
        // Пользователь может удалить все категории, и стартовые ссылки не должны появляться снова.
        var hasLayout = await dbContext.Layouts.AnyAsync(cancellationToken);
        if (hasLayout)
        {
            logger.LogInformation("Раскладка уже существует. Стартовое наполнение не требуется.");
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
    /// Формирует стартовую раскладку dashboard со ссылками на публичные сервисы.
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
                    Title = "Медиа",
                    Position = new CategoryPosition { Order = 1, Width = 6 },
                    Cards =
                    [
                        CreateCard(1001, 101, "Google", "www.google.com/", "fa-brands fa-google", "#4285f4", true, 0),
                        CreateCard(1002, 101, "YouTube", "www.youtube.com/", "fa-brands fa-youtube", "#ff0000", true, 1),
                        CreateCard(1003, 101, "Wikipedia", "www.wikipedia.org/", "fa-brands fa-wikipedia-w", "#334155", false, 2),
                        CreateCard(1004, 101, "Gmail", "mail.google.com/", "fa-solid fa-envelope", "#ea4335", false, 3)
                    ]
                },
                new DashboardCategory
                {
                    Id = 102,
                    DashboardLayoutId = 1,
                    Title = "Работа и код",
                    Position = new CategoryPosition { Order = 2, Width = 6 },
                    Cards =
                    [
                        CreateCard(2001, 102, "Adashboard на GitHub", "github.com/tjden88/Adashboard", "fa-brands fa-github", "#24292f", true, 0, sm: CardStatusMode.Reachable),
                        CreateCard(2002, 102, "Stack Overflow", "stackoverflow.com/", "fa-brands fa-stack-overflow", "#f48024", false, 1),
                    ]
                },
                new DashboardCategory
                {
                    Id = 103,
                    DashboardLayoutId = 1,
                    Title = "Домашний сервер",
                    Position = new CategoryPosition { Order = 0, Width = 12 },
                    Cards =
                    [
                        CreateCard(3001, 103, "NAS", "nas.local/", "fa-solid fa-hard-drive", "#334155", true, 0, "http://", sm: CardStatusMode.Reachable),
                        CreateCard(3002, 103, "Proxmox", "proxmox.local/", "fa-solid fa-server", "#e57000", true, 1, "http://"),
                        CreateCard(3003, 103, "Home Assistant", "homeassistant.local/", "fa-solid fa-house", "#18bcf2", false, 2, "http://", sm: CardStatusMode.Reachable),
                        CreateCard(3004, 103, "Jellyfin", "jellyfin.local/", "fa-solid fa-film", "#a78bfa", false, 3, "http://"),
                        CreateCard(3005, 103, "Nextcloud", "nextcloud.local/", "fa-solid fa-cloud", "#0082c9", false, 4, "http://"),
                        CreateCard(3006, 103, "Pi-hole", "pihole.local/", "fa-solid fa-shield-halved", "#b91c1c", false, 5, "http://"),
                        CreateCard(3007, 103, "Portainer", "portainer.local/", "fa-brands fa-docker", "#0ea5e9", false, 6, "http://"),
                        CreateCard(3008, 103, "Gitea", "gitea.local/", "fa-brands fa-git-alt", "#609926", true, 7, "http://")
                    ]
                }
            ]
        };

        return layout;
    }

    /// <summary>
    /// Создаёт карточку с заданным идентификатором, схемой ссылки и позицией.
    /// </summary>
    private static DashboardCard CreateCard(int id,
        int categoryId,
        string title,
        string url,
        string iconClass,
        string backgroundColor,
        bool isWide,
        int order,
        string urlScheme = "https://",
        CardStatusMode sm = CardStatusMode.None) =>
        new()
        {
            Id = id,
            CategoryId = categoryId,
            Title = title,
            Url = url,
            UrlScheme = urlScheme,
            IconClass = iconClass,
            BackgroundColor = backgroundColor,
            IsWide = isWide,
            Position = new CardPosition { Order = order },
            StatusMode = sm
        };
}
