using Adashboard.Data;
using Adashboard.Data.Entities;
using Adashboard.Domain.Contracts;
using Adashboard.Domain.ReadModels;
using Adashboard.Domain.Reference;
using Mapster;
using Microsoft.EntityFrameworkCore;
using WPR.Operations;

namespace Adashboard.Server.Services;

/// <summary>
/// Сервис работы со страницами dashboard: чтение, создание, изменение информационной части,
/// удаление, а также CRUD категорий и карточек. Работает с контрактными моделями,
/// наружу сущности не отдаёт.
/// </summary>
public sealed class PageService(DashboardDb db, ILogger<PageService> logger, ImageIconService iconService)
{
    /// <summary>
    /// Возвращает список всех страниц dashboard.
    /// </summary>
    public async Task<List<DashboardPage>> GetPagesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата загрузка списка страниц dashboard.");

        // Порядок в меню задаёт Order; Id в качестве второго ключа держит страницы с одинаковым
        // порядком (например, в базе до появления порядка у всех 0) в предсказуемом виде.
        var pages = await db.Pages
            .AsNoTracking()
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ProjectToType<DashboardPage>()
            .ToListAsync(cancellationToken);

        logger.LogDebug("Загружено страниц dashboard: {Count}.", pages.Count);
        return pages;
    }

    /// <summary>
    /// Загружает все страницы dashboard с категориями и карточками в порядке меню.
    /// </summary>
    public async Task<List<DashboardPageReadModel>> GetFullPagesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата загрузка всех страниц dashboard с категориями и карточками.");

        // Один запрос вместо N+1: связанная загрузка со split query отдаёт страницы,
        // их категории и карточки сразу. Порядок задаёт меню, внутри страницы — порядок
        // категории и положение карточки.
        var entities = await db.Pages
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Categories)
            .ThenInclude(x => x.Cards)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var pages = entities.Select(x => new DashboardPageReadModel
        {
            Page = x.Adapt<DashboardPage>(),
            Categories = x.Categories.OrderBy(c => c.Order).Adapt<List<DashboardCategory>>(),
            Cards = x.Categories.SelectMany(c => c.Cards).OrderBy(c => c.Position).Adapt<List<DashboardCard>>()
        }).ToList();

        logger.LogDebug("Загружено страниц dashboard с содержимым: {Count}.", pages.Count);
        return pages;
    }

    /// <summary>
    /// Загружает страницу dashboard вместе с категориями и карточками.
    /// </summary>
    public async Task<OperationResult<DashboardPageReadModel>> GetPageAsync(int id, CancellationToken cancellationToken = default)
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
            return Result.Failure("Страница не найдена.").As<DashboardPageReadModel>();
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
    public async Task<OperationResult<DashboardPage>> CreatePageAsync(DashboardPage page, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато создание страницы dashboard {Title}.", page.Title);

        var path = page.Path.Trim();

        if (await db.Pages.AnyAsync(x => x.Path == path, cancellationToken))
        {
            logger.LogWarning("Создание страницы отклонено: путь {Path} уже занят.", path);
            return Result.Failure("Страница с таким путём уже существует.").As<DashboardPage>();
        }

        var entity = page.Adapt<DashboardPageEntity>();
        entity.Id = 0; // Идентификатор назначает EF Core
        entity.Path = path;
        // Новая страница всегда встаёт в конец меню: порядок существующих страниц при этом не меняется.
        entity.Order = await db.Pages.MaxAsync(x => (int?)x.Order, cancellationToken) ?? 0;
        entity.Order++;

        db.Pages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Страница dashboard создана. Идентификатор: {PageId}.", entity.Id);
        return entity.Adapt<DashboardPage>();
    }

    /// <summary>
    /// Обновляет информационную часть страницы dashboard (заголовок, описание и путь).
    /// </summary>
    public async Task<OperationResult> UpdatePageInfoAsync(DashboardPage page, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато изменение информационной части страницы dashboard {PageId}.", page.Id);

        var entity = await db.Pages.FirstOrDefaultAsync(x => x.Id == page.Id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение страницы отклонено: страница dashboard с id {PageId} не найдена.", page.Id);
            return Result.Failure("Страница не найдена.");
        }

        var path = page.Path.Trim();
        if (!string.Equals(path, entity.Path, StringComparison.Ordinal))
        {
            // Пустой путь зарезервирован за основной страницей: занять его или изменить её путь нельзя.
            if (entity.Path.Length == 0 || path.Length == 0)
            {
                logger.LogWarning("Изменение страницы {PageId} отклонено: путь основной страницы зарезервирован.", page.Id);
                return Result.Failure("Путь основной страницы изменить нельзя.");
            }

            var pathTaken = await db.Pages.AnyAsync(x => x.Id != page.Id && x.Path == path, cancellationToken);
            if (pathTaken)
            {
                logger.LogWarning("Изменение страницы {PageId} отклонено: путь {Path} уже занят.", page.Id, path);
                return Result.Failure("Страница с таким путём уже существует.");
            }
        }

        // Порядок в меню принадлежит операции перестановки: диалог настроек страницы его не меняет,
        // поэтому сохраняем значение из базы.
        var currentOrder = entity.Order;

        page.Adapt(entity);
        entity.Path = path;
        entity.Order = currentOrder;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Информационная часть страницы dashboard {PageId} обновлена.", page.Id);
        return Result.Success();
    }

    /// <summary>
    /// Применяет новый порядок страниц в боковом меню.
    /// </summary>
    public async Task<OperationResult> ReorderPagesAsync(IReadOnlyList<int> orderedPageIds, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата перестановка страниц dashboard: {Count}.", orderedPageIds.Count);

        if (orderedPageIds.Count == 0)
        {
            logger.LogDebug("Список страниц для перестановки пуст: перестановка не требуется.");
            return Result.Success();
        }

        var entities = await db.Pages.ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            logger.LogWarning("Перестановка страниц отклонена: страниц dashboard в базе нет.");
            return Result.Failure("Страницы не найдены.");
        }

        var lookup = entities.ToDictionary(x => x.Id);
        var appliedIds = new HashSet<int>();

        // Основная страница (пустой путь) всегда остаётся первой и всегда имеет порядок 0:
        // переставить её нельзя, поэтому ей не выделяется позиция в присланном списке.
        var mainPage = entities.FirstOrDefault(x => x.Path.Length == 0);
        var movableOrder = 1;

        foreach (var id in orderedPageIds)
        {
            if (!lookup.TryGetValue(id, out var entity))
            {
                logger.LogWarning("Перестановка страниц отклонена: идентификатор {PageId} не относится к страницам dashboard.", id);
                return Result.Failure("Не все страницы найдены.");
            }

            // Повторный идентификатор означал бы потерю другой страницы из меню.
            if (!appliedIds.Add(id))
            {
                logger.LogWarning("Перестановка страниц отклонена: идентификатор {PageId} встречается в списке дважды.", id);
                return Result.Failure("В списке перестановки есть повторяющиеся страницы.");
            }

            if (entity.Path.Length == 0)
                continue;

            entity.Order = movableOrder;
            movableOrder++;
        }

        if (mainPage is not null)
            mainPage.Order = 0;

        // Страницы, которых нет в присланном списке, остаются на прежних местах, но получают
        // свободные позиции в конце: порядок в меню не должен содержать пропусков и дублей.
        foreach (var entity in entities.OrderBy(x => x.Order).ThenBy(x => x.Id))
        {
            if (entity.Path.Length == 0)
                continue;

            if (orderedPageIds.Contains(entity.Id))
                continue;

            entity.Order = movableOrder;
            movableOrder++;
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Новый порядок страниц dashboard применён. Переставлено страниц: {Count}.", orderedPageIds.Count);
        return Result.Success();
    }

    /// <summary>
    /// Удаляет страницу dashboard вместе с её категориями и карточками.
    /// </summary>
    public async Task<OperationResult> DeletePageAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато удаление страницы dashboard {PageId}.", id);

        var entity = await db.Pages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Удаление страницы {PageId} отклонено: страница не найдена.", id);
            return Result.Failure("Страница не найдена.");
        }

        if (entity.Path.Length == 0)
        {
            logger.LogWarning("Удаление страницы {PageId} отклонено: основную страницу удалить нельзя.", id);
            return Result.Failure("Основную страницу удалить нельзя.");
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
            return Result.Failure("Страница не найдена.");
        }

        foreach (var cardId in cardsWithCustomImagesIds)
        {
            iconService.Delete(cardId);
        }

        logger.LogInformation("Страница dashboard {PageId} удалена.", id);
        return Result.Success();
    }

    /// <summary>
    /// Возвращает все категории dashboard плоским списком с идентификатором страницы: клиент сам
    /// группирует их по страницам, поэтому порядок задаёт страница и порядок категории внутри неё.
    /// </summary>
    public async Task<List<DashboardCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата загрузка списка категорий dashboard.");

        var categories = await db.Categories
            .AsNoTracking()
            .OrderBy(x => x.DashboardPageId)
            .ThenBy(x => x.Order)
            .ProjectToType<DashboardCategory>()
            .ToListAsync(cancellationToken);

        logger.LogDebug("Загружено категорий dashboard: {Count}.", categories.Count);
        return categories;
    }

    /// <summary>
    /// Создаёт новую категорию на указанной странице.
    /// </summary>
    public async Task<OperationResult<DashboardCategory>> CreateCategoryAsync(DashboardCategory category, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато создание категории {Title}.", category.Title);

        // Проверим существование страницы.
        var pageExists = await db.Pages.AnyAsync(x => x.Id == category.DashboardPageId, cancellationToken);
        if (!pageExists)
        {
            logger.LogWarning("Создание категории отклонено: страница dashboard с id {PageId} не найдена.", category.DashboardPageId);
            return Result.Failure("Страница не найдена.").As<DashboardCategory>();
        }

        category.MobileWidth = NormalizeMobileWidth(category.MobileWidth);

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
    public async Task<OperationResult> UpdateCategoryAsync(DashboardCategory category, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато изменение категории {CategoryId}.", category.Id);

        var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == category.Id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение категории {CategoryId} отклонено: категория не найдена.", category.Id);
            return Result.Failure("Категория не найдена.");
        }

        category.MobileWidth = NormalizeMobileWidth(category.MobileWidth);
        category.Adapt(entity);

        // Проверим существование страницы, на которую переносится категория.
        var pageExists = await db.Pages.AnyAsync(c => c.Id == entity.DashboardPageId, cancellationToken);

        if (!pageExists)
        {
            logger.LogWarning("Изменение категории {CategoryId} отклонено: страница {PageId} не найдена.", category.Id, entity.DashboardPageId);
            return Result.Failure("Страница не найдена.");
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Категория {CategoryId} обновлена.", category.Id);
        return Result.Success();
    }

    /// <summary>
    /// Удаляет категорию вместе с её карточками.
    /// </summary>
    public async Task<OperationResult> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато удаление категории {CategoryId}.", id);

        var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Удаление категории {CategoryId} отклонено: категория не найдена.", id);
            return Result.Failure("Категория не найдена.");
        }

        // Получаем идентификаторы карточек с кастомными изображениями, чтобы удалить их файлы после удаления категории.
        var cardsWithCustomImagesIds = await db.Cards
            .Where(c => c.DashboardCategoryId == id && c.IsCustomIcon)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);


        // Карточки категории удаляются каскадно на уровне БД (обязательная связь).
        db.Categories.Remove(entity);
        if (await db.SaveChangesAsync(cancellationToken) == 0)
        {
            logger.LogWarning("Удаление категории {CategoryId} отклонено базой данных.", id);
            return Result.Failure("Категория не найдена.");
        }

        // Удаляем файлы кастомных изображений карточек после успешного удаления категории и её карточек.
        foreach (var cardId in cardsWithCustomImagesIds)
        {
            iconService.Delete(cardId);
        }

        logger.LogInformation("Категория {CategoryId} удалена.", id);
        return Result.Success();
    }

    /// <summary>
    /// Создаёт копию категории со всеми карточками в той же странице: копия встаёт сразу после
    /// исходной, поэтому порядок последующих категорий страницы сдвигается на одну.
    /// </summary>
    public async Task<OperationResult<DashboardCategoryReadModel>> DuplicateCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато дублирование категории {CategoryId}.", id);

        var source = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (source is null)
        {
            logger.LogWarning("Дублирование категории {CategoryId} отклонено: категория не найдена.", id);
            return Result.Failure("Категория не найдена.").As<DashboardCategoryReadModel>();
        }

        // Карточки читаем отдельным запросом: навигацию EF здесь не подгружаем, а положение
        // копии сохраняют такое же, как у исходных.
        var sourceCards = await db.Cards
            .Where(x => x.DashboardCategoryId == id)
            .OrderBy(x => x.Position)
            .ToListAsync(cancellationToken);

        // Сдвиг делаем до добавления копии: иначе обновление затронуло бы и её саму
        // (копия получает порядок сразу после исходной).
        await db.Categories
            .Where(x => x.DashboardPageId == source.DashboardPageId && x.Order > source.Order)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Order, p => p.Order + 1), cancellationToken);

        var copy = source.Adapt<DashboardCategoryEntity>();
        copy.Id = 0; // Идентификатор назначает EF Core
        copy.Order = source.Order + 1;

        // Копию сохраняем отдельно: идентификатор категории назначает EF Core, и только после
        // этого карточки можно привязать к новой категории.
        db.Categories.Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        // Файл изображения-иконки назван по идентификатору карточки, поэтому копия ссылается на
        // файл исходной: новый файл не создаём. Положение внутри категории копия сохраняет.
        var copyCards = new List<DashboardCardEntity>(sourceCards.Count);
        foreach (var card in sourceCards)
        {
            var copyCard = card.Adapt<DashboardCardEntity>();
            copyCard.Id = 0; // Идентификатор назначает EF Core
            copyCard.DashboardCategoryId = copy.Id;

            db.Cards.Add(copyCard);
            copyCards.Add(copyCard);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Категория {CategoryId} продублирована. Идентификатор копии: {CopyCategoryId}. Карточек: {CardCount}.", id, copy.Id, copyCards.Count);

        return new DashboardCategoryReadModel
        {
            Category = copy.Adapt<DashboardCategory>(),
            Cards = copyCards.Adapt<List<DashboardCard>>()
        };
    }

    /// <summary>
    /// Создаёт новую карточку в указанной категории, при необходимости с изображением-иконкой.
    /// </summary>
    public async Task<OperationResult<DashboardCard>> CreateCardAsync(
        DashboardCard card,
        byte[]? imageContent,
        CancellationToken cancellationToken = default)
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
            return Result.Failure("Категория не найдена.").As<DashboardCard>();
        }

        var entity = card.Adapt<DashboardCardEntity>();
        entity.Id = 0;
        entity.Position = category.Count; // Новая карточка добавляется в конец списка

        // Изображение-иконку сохраняет отдельный сервис, а имя файла содержит идентификатор
        // карточки: сначала создаём запись, и только потом привязываем к ней файл.
        var newImage = imageContent is { Length: > 0 } content ? content : null;
        if (newImage is not null)
        {
            entity.IsCustomIcon = true;
            entity.IconSource = string.Empty;
        }

        db.Cards.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        if (newImage is not null)
        {
            var savedIcon = await iconService.SaveFromBytesAsync(entity.Id, newImage, cancellationToken);
            if (!savedIcon.IsSuccess)
            {
                // Карточка без запрошенной иконки оставлять нельзя: убираем запись вместе с ошибкой.
                db.Cards.Remove(entity);
                await db.SaveChangesAsync(cancellationToken);

                logger.LogWarning("Создание карточки {CardId} отменено: изображение не сохранено.", entity.Id);
                return Result.Failure(savedIcon.Error).As<DashboardCard>();
            }

            entity.IconSource = savedIcon.Value;
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Карточка создана. Идентификатор: {CardId}.", entity.Id);
        return entity.Adapt<DashboardCard>();
    }

    /// <summary>
    /// Изменяет существующую карточку, в том числе переносит её в другую категорию, при
    /// необходимости заменяя или удаляя изображение-иконку.
    /// </summary>
    public async Task<OperationResult<DashboardCard>> UpdateCardAsync(
        DashboardCard card,
        byte[]? imageContent,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато изменение карточки {CardId}.", card.Id);

        var entity = await db.Cards.FirstOrDefaultAsync(x => x.Id == card.Id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение карточки {CardId} отклонено: карточка не найдена.", card.Id);
            return Result.Failure("Карточка не найдена.").As<DashboardCard>();
        }

        // Категория до изменения нужна, чтобы отличить перенос карточки в другую категорию
        // от обычного сохранения: положение задаёт целевая категория, а прежние координаты
        // в старой категории теряют смысл.
        var previousCategoryId = entity.DashboardCategoryId;

        card.Adapt(entity);

        // Проверим существование категории.
        var categoryExists = await db.Categories.AnyAsync(c => c.Id == entity.DashboardCategoryId, cancellationToken);

        if (!categoryExists)
        {
            logger.LogWarning("Изменение карточки {CardId} отклонено: категория {CategoryId} не найдена.", card.Id, entity.DashboardCategoryId);
            return Result.Failure("Категория не найдена.").As<DashboardCard>();
        }

        if (entity.DashboardCategoryId != previousCategoryId)
        {
            // Перенесённая карточка встаёт в конец целевой категории — так же, как новая.
            // Карточка в запросе ещё относится к старой категории, поэтому максимум считаем
            // только по карточкам целевой.
            var lastPosition = await db.Cards
                .Where(x => x.DashboardCategoryId == entity.DashboardCategoryId)
                .MaxAsync(x => (int?)x.Position, cancellationToken);

            entity.Position = (lastPosition ?? -1) + 1;
            logger.LogInformation("Карточка {CardId} перенесена в категорию {CategoryId}. Положение: {Position}.", card.Id, entity.DashboardCategoryId, entity.Position);
        }

        // Новое изображение заменяет прежний файл иконки, отказ от пользовательской иконки — удаляет его.
        if (imageContent is { Length: > 0 })
        {
            var savedIcon = await iconService.SaveFromBytesAsync(entity.Id, imageContent, cancellationToken);
            if (!savedIcon.IsSuccess)
            {
                logger.LogWarning("Изменение карточки {CardId} отменено: изображение не сохранено.", entity.Id);
                return Result.Failure(savedIcon.Error).As<DashboardCard>();
            }

            entity.IsCustomIcon = true;
            entity.IconSource = savedIcon.Value;
        }
        else if (!card.IsCustomIcon)
        {
            iconService.Delete(card.Id);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Карточка {CardId} обновлена.", card.Id);
        return entity.Adapt<DashboardCard>();
    }

    /// <summary>
    /// Создаёт копию карточки в той же категории: копия встаёт сразу после исходной,
    /// поэтому положение последующих карточек категории сдвигается на одну.
    /// </summary>
    public async Task<OperationResult<DashboardCard>> DuplicateCardAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато дублирование карточки {CardId}.", id);

        var source = await db.Cards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (source is null)
        {
            logger.LogWarning("Дублирование карточки {CardId} отклонено: карточка не найдена.", id);
            return Result.Failure("Карточка не найдена.").As<DashboardCard>();
        }

        // Сдвиг делаем до добавления копии: иначе обновление затронуло бы и её саму
        // (копия получает положение сразу после исходной).
        await db.Cards
            .Where(x => x.DashboardCategoryId == source.DashboardCategoryId && x.Position > source.Position)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Position, p => p.Position + 1), cancellationToken);

        var copy = source.Adapt<DashboardCardEntity>();
        copy.Id = 0; // Идентификатор назначает EF Core
        copy.Position = source.Position + 1;

        // Файл изображения-иконки назван по идентификатору карточки, поэтому копия ссылается на
        // файл исходной: новый файл не создаём.
        db.Cards.Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Карточка {CardId} продублирована. Идентификатор копии: {CopyCardId}.", id, copy.Id);
        return copy.Adapt<DashboardCard>();
    }

    /// <summary>
    /// Удаляет карточку.
    /// </summary>
    public async Task<OperationResult> DeleteCardAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато удаление карточки {CardId}.", id);

        var entity = await db.Cards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Удаление карточки {CardId} отклонено: карточка не найдена.", id);
            return Result.Failure("Карточка не найдена.");
        }

        db.Cards.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        // Удаляем файл кастомного изображения карточки после успешного удаления карточки.
        if (entity.IsCustomIcon) iconService.Delete(id);

        logger.LogInformation("Карточка {CardId} удалена.", id);
        return Result.Success();
    }

    /// <summary>
    /// Приводит мобильную ширину категории к допустимому диапазону: пустое значение остаётся пустым
    /// (мобильная ширина тогда производная от десктопной), число ограничивается колонками мобильной сетки.
    /// </summary>
    private static int? NormalizeMobileWidth(int? mobileWidth)
    {
        if (mobileWidth is null) return null;
        return Math.Clamp(mobileWidth.Value, DomainConstants.CategoryWidthMin, DomainConstants.CategoryMobileWidthMax);
    }
}
