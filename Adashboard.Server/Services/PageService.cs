using Adashboard.Data;
using Adashboard.Data.Entities;
using Adashboard.Domain.Contracts;
using Adashboard.Domain.ReadModels;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Server.Services;

/// <summary>
/// Сервис работы со страницами dashboard: чтение, создание, изменение информационной части,
/// удаление, а также CRUD категорий и карточек. Работает с контрактными моделями,
/// наружу сущности не отдаёт.
/// </summary>
/// <remarks>
/// Маппинг «сущность ↔ контрактная модель» выполняет Mapster по правилам из
/// <see cref="Adashboard.Server.Mapping.MapperConfig"/>.
/// Основная страница имеет пустой путь; её нельзя удалить и нельзя изменить её путь.
/// </remarks>
public sealed class PageService(DashboardDb db, ILogger<PageService> logger, ImageIconService iconService)
{
    /// <summary>
    /// Возвращает список всех страниц dashboard.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public async Task<List<DashboardPage>> GetPagesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата загрузка списка страниц dashboard.");

        var pages = await db.Pages
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ProjectToType<DashboardPage>()
            .ToListAsync(cancellationToken);

        logger.LogDebug("Загружено страниц dashboard: {Count}.", pages.Count);
        return pages;
    }

    /// <summary>
    /// Загружает страницу dashboard вместе с категориями и карточками.
    /// </summary>
    /// <param name="id">Идентификатор страницы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Read-модель страницы либо <c>null</c>, если страница не найдена.</returns>
    public async Task<DashboardPageReadModel?> GetPageAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата загрузка страницы dashboard с ID {PageId} из базы данных.", id);

        var entity = await db.Pages
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Categories)
            .ThenInclude(x => x.Cards)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Страница dashboard с ID {PageId} не найдена в базе данных.", id);
            return null;
        }

        var readModel = new DashboardPageReadModel
        {
            Page = entity.Adapt<DashboardPage>(),
            Categories = entity.Categories
                .OrderBy(c => c.Order)
                .Adapt<List<DashboardCategory>>(),
            Cards = entity.Categories
                .SelectMany(c => c.Cards)
                .OrderBy(c => c.Position)
                .Adapt<List<DashboardCard>>()
        };

        logger.LogDebug("Страница dashboard с ID {PageId} загружена.", id);
        return readModel;
    }

    /// <summary>
    /// Создаёт новую страницу dashboard.
    /// </summary>
    /// <param name="page">Контрактная модель создаваемой страницы. Путь обязателен и уникален.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная страница с назначенным идентификатором либо <c>null</c>, если путь уже занят.</returns>
    public async Task<DashboardPage?> CreatePageAsync(DashboardPage page, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато создание страницы dashboard {Title}.", page.Title);

        var path = page.Path.Trim();

        if (await db.Pages.AnyAsync(x => x.Path == path, cancellationToken))
        {
            logger.LogWarning("Создание страницы отклонено: путь {Path} уже занят.", path);
            return null;
        }

        var entity = page.Adapt<DashboardPageEntity>();
        entity.Id = 0; // Идентификатор назначает EF Core
        entity.Path = path;

        db.Pages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Страница dashboard создана. Идентификатор: {PageId}.", entity.Id);
        return entity.Adapt<DashboardPage>();
    }

    /// <summary>
    /// Обновляет информационную часть страницы dashboard (заголовок, описание и путь).
    /// </summary>
    /// <param name="page">Плоская модель страницы с новыми значениями; <c>Id</c> задаёт изменяемую страницу.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат операции: успех, страница не найдена, путь занят или путь основной страницы зарезервирован.</returns>
    public async Task<PageMutationResult> UpdatePageInfoAsync(DashboardPage page, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато изменение информационной части страницы dashboard {PageId}.", page.Id);

        var entity = await db.Pages.FirstOrDefaultAsync(x => x.Id == page.Id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение страницы отклонено: страница dashboard с id {PageId} не найдена.", page.Id);
            return PageMutationResult.NotFound;
        }

        var path = page.Path.Trim();
        if (!string.Equals(path, entity.Path, StringComparison.Ordinal))
        {
            // Пустой путь зарезервирован за основной страницей: занять его или изменить её путь нельзя.
            if (entity.Path.Length == 0 || path.Length == 0)
            {
                logger.LogWarning("Изменение страницы {PageId} отклонено: путь основной страницы зарезервирован.", page.Id);
                return PageMutationResult.MainPageProtected;
            }

            var pathTaken = await db.Pages.AnyAsync(x => x.Id != page.Id && x.Path == path, cancellationToken);
            if (pathTaken)
            {
                logger.LogWarning("Изменение страницы {PageId} отклонено: путь {Path} уже занят.", page.Id, path);
                return PageMutationResult.PathTaken;
            }
        }

        page.Adapt(entity);
        entity.Path = path;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Информационная часть страницы dashboard {PageId} обновлена.", page.Id);
        return PageMutationResult.Success;
    }

    /// <summary>
    /// Удаляет страницу dashboard вместе с её категориями и карточками.
    /// </summary>
    /// <param name="id">Идентификатор страницы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат операции.</returns>
    public async Task<PageMutationResult> DeletePageAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато удаление страницы dashboard {PageId}.", id);

        var entity = await db.Pages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Удаление страницы {PageId} отклонено: страница не найдена.", id);
            return PageMutationResult.NotFound;
        }

        if (entity.Path.Length == 0)
        {
            logger.LogWarning("Удаление страницы {PageId} отклонено: основную страницу удалить нельзя.", id);
            return PageMutationResult.MainPageProtected;
        }

        // Собираем карточки с кастомными изображениями, чтобы удалить их файлы после удаления страницы.
        var cardsWithCustomImagesIds = await db.Cards
            .Where(c => c.Category!.DashboardPageId == id && c.IsCustomIcon)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        // Категории и карточки удаляются каскадно на уровне БД.
        db.Pages.Remove(entity);
        if (await db.SaveChangesAsync(cancellationToken) == 0)
        {
            logger.LogWarning("Удаление страницы {PageId} отклонено базой данных.", id);
            return PageMutationResult.NotFound;
        }

        foreach (var cardId in cardsWithCustomImagesIds)
        {
            iconService.Delete(cardId);
        }

        logger.LogInformation("Страница dashboard {PageId} удалена.", id);
        return PageMutationResult.Success;
    }

    /// <summary>
    /// Создаёт новую категорию на указанной странице.
    /// </summary>
    /// <param name="category">Контрактная модель создаваемой категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная категория с назначенным идентификатором.</returns>
    public async Task<DashboardCategory?> CreateCategoryAsync(DashboardCategory category, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато создание категории {Title}.", category.Title);

        // Проверим существование страницы.
        var pageExists = await db.Pages.AnyAsync(x => x.Id == category.DashboardPageId, cancellationToken);
        if (!pageExists)
        {
            logger.LogWarning("Создание категории отклонено: страница dashboard с id {PageId} не найдена.", category.DashboardPageId);
            return null;
        }

        var entity = category.Adapt<DashboardCategoryEntity>();
        // Идентификатор назначает EF Core
        entity.Id = 0;

        db.Categories.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Категория создана. Идентификатор: {CategoryId}.", entity.Id);
        return entity.Adapt<DashboardCategory>();
    }

    /// <summary>
    /// Изменяет существующую категорию, в том числе переносит её на другую страницу.
    /// </summary>
    /// <param name="category">Новые значения полей категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><c>true</c>, если категория найдена и обновлена; иначе <c>false</c>.</returns>
    public async Task<bool> UpdateCategoryAsync(DashboardCategory category, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато изменение категории {CategoryId}.", category.Id);

        var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == category.Id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение категории {CategoryId} отклонено: категория не найдена.", category.Id);
            return false;
        }

        category.Adapt(entity);

        // Проверим существование страницы, на которую переносится категория.
        var pageExists = await db.Pages.AnyAsync(c => c.Id == entity.DashboardPageId, cancellationToken);

        if (!pageExists)
        {
            logger.LogWarning("Изменение категории {CategoryId} отклонено: страница {PageId} не найдена.", category.Id, entity.DashboardPageId);
            return false;
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Категория {CategoryId} обновлена.", category.Id);
        return true;
    }

    /// <summary>
    /// Удаляет категорию вместе с её карточками.
    /// </summary>
    /// <param name="id">Идентификатор категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><c>true</c>, если категория найдена и удалена; иначе <c>false</c>.</returns>
    public async Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато удаление категории {CategoryId}.", id);

        var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Удаление категории {CategoryId} отклонено: категория не найдена.", id);
            return false;
        }

        // Получаем идентификаторы карточек с кастомными изображениями, чтобы удалить их файлы после удаления категории.
        var cardsWithCustomImagesIds = await db.Cards
            .Where(c => c.DashboardCategoryId == id && c.IsCustomIcon)
            .Select(c=>c.Id)
            .ToListAsync(cancellationToken);


        // Карточки категории удаляются каскадно на уровне БД (обязательная связь).
        db.Categories.Remove(entity);
        if(await db.SaveChangesAsync(cancellationToken) == 0)
        {
            logger.LogWarning("Удаление категории {CategoryId} отклонено базой данных.", id);
            return false;
        }

        // Удаляем файлы кастомных изображений карточек после успешного удаления категории и её карточек.
        foreach (var cardId in cardsWithCustomImagesIds)
        {
            iconService.Delete(cardId);
        }

        logger.LogInformation("Категория {CategoryId} удалена.", id);
        return true;
    }

    /// <summary>
    /// Создаёт новую карточку в указанной категории.
    /// </summary>
    /// <param name="card">Контрактная модель создаваемой карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная карточка с назначенным идентификатором либо <c>null</c>, если категория не найдена.</returns>
    public async Task<DashboardCard?> CreateCardAsync(DashboardCard card, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато создание карточки {Title} в категории {CategoryId}.", card.Title, card.DashboardCategoryId);

        var category = await db.Categories
            .Select(c => new
            {
                c.Id,
                c.Cards.Count
            })
            .FirstOrDefaultAsync(x => x.Id == card.DashboardCategoryId, cancellationToken);
        if (category is null)
        {
            logger.LogWarning("Создание карточки отклонено: категория {CategoryId} не найдена.", card.DashboardCategoryId);
            return null;
        }

        var entity = card.Adapt<DashboardCardEntity>();
        entity.Id = 0;
        entity.Position = category.Count; // Новая карточка добавляется в конец списка

        db.Cards.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Карточка создана. Идентификатор: {CardId}.", entity.Id);
        return entity.Adapt<DashboardCard>();
    }

    /// <summary>
    /// Изменяет существующую карточку.
    /// </summary>
    /// <param name="card">Новые значения полей карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><c>true</c>, если карточка найдена и обновлена; иначе <c>false</c>.</returns>
    public async Task<bool> UpdateCardAsync(DashboardCard card, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато изменение карточки {CardId}.", card.Id);

        var entity = await db.Cards.FirstOrDefaultAsync(x => x.Id == card.Id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение карточки {CardId} отклонено: карточка не найдена.", card.Id);
            return false;
        }

        card.Adapt(entity);

        // Проверим существование категории.
        var categoryExists = await db.Categories.AnyAsync(c => c.Id == entity.DashboardCategoryId, cancellationToken);

        if (!categoryExists)
        {
            logger.LogWarning("Изменение карточки {CardId} отклонено: категория {CategoryId} не найдена.", card.Id, entity.DashboardCategoryId);
            return false;
        }

        // Проверим иконку
        if(!card.IsCustomIcon)
            iconService.Delete(card.Id);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Карточка {CardId} обновлена.", card.Id);
        return true;
    }

    /// <summary>
    /// Удаляет карточку.
    /// </summary>
    /// <param name="id">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><c>true</c>, если карточка найдена и удалена; иначе <c>false</c>.</returns>
    public async Task<bool> DeleteCardAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато удаление карточки {CardId}.", id);

        var entity = await db.Cards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Удаление карточки {CardId} отклонено: карточка не найдена.", id);
            return false;
        }

        db.Cards.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        // Удаляем файл кастомного изображения карточки после успешного удаления карточки.
        if (entity.IsCustomIcon) iconService.Delete(id);

        logger.LogInformation("Карточка {CardId} удалена.", id);
        return true;
    }
}
