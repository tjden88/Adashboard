using Adashboard.Models.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Data;

/// <summary>
/// Реализует операции чтения и сохранения раскладки dashboard в SQLite.
/// </summary>
public sealed class DashboardLayoutService(
    IDbContextFactory<DashboardDbContext> dbContextFactory,
    ILogger<DashboardLayoutService> logger) : IDashboardLayoutService
{
    /// <summary>
    /// Загружает основную раскладку dashboard вместе с категориями и карточками.
    /// </summary>
    public async Task<DashboardLayout> GetMainLayoutAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начата загрузка основной раскладки dashboard из базы данных.");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var layout = await dbContext.Layouts
            .AsNoTracking()
            .Include(x => x.Categories)
            .ThenInclude(x => x.Cards)
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (layout is null)
        {
            logger.LogWarning("Основная раскладка dashboard не найдена в базе данных.");
            return new DashboardLayout { Id = 1, ThemeMode = "auto" };
        }

        logger.LogInformation("Основная раскладка dashboard загружена: категорий {CategoryCount}.", layout.Categories.Count);
        return layout;
    }

    /// <summary>
    /// Полностью перезаписывает основную раскладку dashboard в базе данных.
    /// </summary>
    public async Task SaveMainLayoutAsync(DashboardLayout layout, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато сохранение основной раскладки dashboard. Идентификатор: {LayoutId}.", layout.Id);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Удаление и вставка выполняются в одной транзакции: иначе сбой на вставке
        // оставил бы базу без раскладки.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existing = await dbContext.Layouts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == layout.Id, cancellationToken);

        if (existing is not null)
        {
            logger.LogDebug("Найдена существующая раскладка dashboard. Выполняется перезапись.");
            dbContext.Layouts.Remove(new DashboardLayout { Id = existing.Id });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        dbContext.Layouts.Add(CloneLayout(layout));
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Сохранение основной раскладки dashboard завершено. Категорий: {CategoryCount}.",
            layout.Categories.Count);
    }

    /// <summary>
    /// Создаёт копию раскладки для корректного сохранения графа сущностей.
    /// </summary>
    private static DashboardLayout CloneLayout(DashboardLayout source)
    {
        var layout = new DashboardLayout
        {
            Id = source.Id,
            ThemeMode = source.ThemeMode,
            SiteTitle = source.SiteTitle,
            SiteDescription = source.SiteDescription,
            Categories = []
        };

        foreach (var category in source.Categories)
        {
            var categoryCopy = new DashboardCategory
            {
                Id = category.Id,
                DashboardLayoutId = source.Id,
                Title = category.Title,
                Position = new CategoryPosition
                {
                    Order = category.Position.Order,
                    Width = category.Position.Width
                },
                Cards = []
            };

            foreach (var card in category.Cards)
            {
                categoryCopy.Cards.Add(new DashboardCard
                {
                    Id = card.Id,
                    CategoryId = category.Id,
                    Title = card.Title,
                    Url = card.Url,
                    IconClass = card.IconClass,
                    BackgroundColor = card.BackgroundColor,
                    IsWide = card.IsWide,
                    IsOnline = card.IsOnline,
                    ShowStatus = card.ShowStatus,
                    Position = new CardPosition
                    {
                        Order = card.Position.Order
                    }
                });
            }

            layout.Categories.Add(categoryCopy);
        }

        return layout;
    }
}
