using Adashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Api.Data;

/// <summary>
/// Инициализирует базу данных: применяет миграции и один раз добавляет стартовое наполнение.
/// </summary>
internal sealed class DbInitializer(DashboardDb db, ILogger<DbInitializer> logger)
{
    /// <summary>
    /// Применяет миграции и добавляет стартовые категории со ссылками, если раскладка ещё не создавалась.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {

        logger.LogInformation("Запущена инициализация базы данных.");

        var hasMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
        if (hasMigrations)
        {
            logger.LogInformation("Применение миграций БД...");

            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Миграции базы данных успешно применены.");
        }
        else
            logger.LogInformation("Нет ожидающих миграций.");


        // Наличие раскладки служит признаком, что первичное наполнение уже выполнялось.
        var hasLayout = await db.Layouts.AnyAsync(cancellationToken);
        if (hasLayout)
        {
            logger.LogInformation("Раскладка уже существует. Стартовое наполнение не требуется.");
            logger.LogInformation("Инициализация базы данных завершена.");
            return;
        }

        // Идентификаторы сущностям не задаём: их назначает EF Core при сохранении.
        var layout = CreateDefaultLayout();
        db.Layouts.Add(layout);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Добавлена стартовая раскладка.");
        logger.LogInformation("Инициализация базы данных завершена.");
    }

    /// <summary>
    /// Формирует стартовую раскладку dashboard со стартовыми категориями и карточками.
    /// </summary>
    private static DashboardLayout CreateDefaultLayout() => new()
    {
        Id = 1,
        Categories = CreateDefaultCategories(),
        Title = "Adashboard",
        Description = "Персональный дашборд для быстрого доступа к сервисам"
    };

    /// <summary>
    /// Создаёт стартовые категории dashboard вместе с вложенными карточками.
    /// </summary>
    private static List<DashboardCategory> CreateDefaultCategories() =>
    [
        new()
        {
            Title = "Медиа",
            Order = 1,
            Width = 6,
            Cards = CreateMediaCards()
        },
        new()
        {
            Title = "Работа и код",
            Order = 2,
            Width = 6,
            Cards = CreateWorkCards()
        },
        new()
        {
            Title = "Домашний сервер",
            Order = 0,
            Width = 12,
            Cards = CreateHomeServerCards()
        }
    ];

    /// <summary>
    /// Создаёт стартовые карточки категории «Медиа».
    /// </summary>
    private static List<DashboardCard> CreateMediaCards() =>
    [
        CreateCard("Google", "www.google.com/", "fa-brands fa-google", "#4285f4", true, 0),
        CreateCard("YouTube", "www.youtube.com/", "fa-brands fa-youtube", "#ff0000", true, 1),
        CreateCard("Wikipedia", "www.wikipedia.org/", "fa-brands fa-wikipedia-w", "#334155", false, 2),
        CreateCard("Gmail", "mail.google.com/", "fa-solid fa-envelope", "#ea4335", false, 3)
    ];

    /// <summary>
    /// Создаёт стартовые карточки категории «Работа и код».
    /// </summary>
    private static List<DashboardCard> CreateWorkCards() =>
    [
        CreateCard("Adashboard на GitHub", "github.com/tjden88/Adashboard", "fa-brands fa-github", "#24292f", true, 0, sm: CardStatusMode.Reachable),
        CreateCard("Stack Overflow", "stackoverflow.com/", "fa-brands fa-stack-overflow", "#f48024", false, 1)
    ];

    /// <summary>
    /// Создаёт стартовые карточки категории «Домашний сервер».
    /// </summary>
    private static List<DashboardCard> CreateHomeServerCards() =>
    [
        CreateCard("NAS", "nas.local/", "fa-solid fa-hard-drive", "#334155", true, 0, UrlScheme.Http, sm: CardStatusMode.Reachable),
        CreateCard("Proxmox", "proxmox.local/", "fa-solid fa-server", "#e57000", true, 1, UrlScheme.Http),
        CreateCard("Home Assistant", "homeassistant.local/", "fa-solid fa-house", "#18bcf2", false, 2, UrlScheme.Http, sm: CardStatusMode.Reachable),
        CreateCard("Jellyfin", "jellyfin.local/", "fa-solid fa-film", "#a78bfa", false, 3, UrlScheme.Http),
        CreateCard("Nextcloud", "nextcloud.local/", "fa-solid fa-cloud", "#0082c9", false, 4, UrlScheme.Http),
        CreateCard("Pi-hole", "pihole.local/", "fa-solid fa-shield-halved", "#b91c1c", false, 5, UrlScheme.Http),
        CreateCard("Portainer", "portainer.local/", "fa-brands fa-docker", "#0ea5e9", false, 6, UrlScheme.Http),
        CreateCard("Gitea", "gitea.local/", "fa-brands fa-git-alt", "#609926", true, 7, UrlScheme.Http)
    ];

    /// <summary>
    /// Создаёт карточку с заданным схемой ссылки и позицией.
    /// </summary>
    private static DashboardCard CreateCard(
        string title,
        string url,
        string iconClass,
        string backgroundColor,
        bool isWide,
        int order,
        UrlScheme urlScheme = UrlScheme.Https,
        CardStatusMode sm = CardStatusMode.None) =>
        new()
        {
            Title = title,
            Url = url,
            UrlScheme = urlScheme,
            IconSource = iconClass,
            BackgroundColor = backgroundColor,
            IsWide = isWide,
            Position = order,
            StatusMode = sm
        };
}
