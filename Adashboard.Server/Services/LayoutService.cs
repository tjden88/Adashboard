using Adashboard.Data;
using Adashboard.Data.Entities;
using Adashboard.Domain.Contracts;
using Adashboard.Domain.ReadModels;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Server.Services;

/// <summary>
/// Сервис работы с раскладкой dashboard: чтение, изменение информационной части,
/// CRUD категорий и карточек. Работает с контрактными моделями, наружу сущности не отдаёт.
/// </summary>
/// <remarks>
/// Маппинг «сущность ↔ контрактная модель» выполняет Mapster по правилам из
/// <see cref="Adashboard.Server.Mapping.MapperConfig"/>.
/// </remarks>
public sealed class LayoutService(DashboardDb db, ILogger<LayoutService> logger, ImageIconService iconService)
{
    /// <summary>
    /// Загружает раскладку dashboard вместе с категориями и карточками.
    /// </summary>
    /// <param name="id">Идентификатор раскладки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Read-модель раскладки</returns>
    public async Task<DashboardLayoutReadModel> GetLayoutAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата загрузка раскладки dashboard с ID {LayoutId} из базы данных.", id);

        var entity = await db.Layouts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Categories)
            .ThenInclude(x => x.Cards)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Раскладка dashboard с ID {LayoutId} не найдена в базе данных.", id);
            return new DashboardLayoutReadModel { Layout = new DashboardLayout { Id = id } };
        }

        var readModel = new DashboardLayoutReadModel
        {
            Layout = entity.Adapt<DashboardLayout>(),
            Categories = entity.Categories
                .OrderBy(c=>c.Order)
                .Adapt<List<DashboardCategory>>(),
            Cards = entity.Categories
                .SelectMany(c => c.Cards)
                .OrderBy(c => c.Position)
                .Adapt<List<DashboardCard>>()
        };

        logger.LogDebug("Раскладка dashboard с ID {LayoutId} загружена.", id);
        return readModel;
    }

    /// <summary>
    /// Добавляет новую раскладку
    /// </summary>
    public async Task<DashboardLayout> AddLayoutAsync(DashboardLayout layout, CancellationToken cancellationToken = default)
    {

        var entity = layout.Adapt<DashboardLayoutEntity>();
        entity.Id = 0; // Идентификатор назначает EF Core
        db.Layouts.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Раскладка dashboard добавлена.");
        return entity.Adapt<DashboardLayout>();
    }

    /// <summary>
    /// Обновляет информационную часть раскладки dashboard (заголовок и описание).
    /// </summary>
    /// <param name="layout">Плоская модель раскладки с новыми заголовком и описанием; <c>Id</c> игнорируется.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><c>true</c>, если раскладка найдена и обновлена; иначе <c>false</c>.</returns>
    public async Task<bool> UpdateLayoutAsync(DashboardLayout layout, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато изменение информационной части раскладки dashboard.");

        var entity = layout.Id == 0 
            ? new DashboardLayoutEntity() 
            : await db.Layouts.FirstOrDefaultAsync(x => x.Id == layout.Id, cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Изменение информационной части отклонено: раскладка dashboard с id {LayoutId} не найдена.", layout.Id);
            return false;
        }

        layout.Adapt(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Информационная часть раскладки dashboard обновлена.");
        return true;
    }

    /// <summary>
    /// Создаёт новую категорию в основной раскладке.
    /// </summary>
    /// <param name="category">Контрактная модель создаваемой категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная категория с назначенным идентификатором.</returns>
    public async Task<DashboardCategory?> CreateCategoryAsync(DashboardCategory category, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато создание категории {Title}.", category.Title);

        // Проверим существование раскладки.
        var layoutExists = await db.Layouts.AnyAsync(x => x.Id == category.DashboardLayoutId, cancellationToken);
        if (!layoutExists)
        {
            logger.LogWarning("Создание категории отклонено: раскладка dashboard с id {LayoutId} не найдена.", category.DashboardLayoutId);
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
    /// Изменяет существующую категорию.
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
    /// <param name="id">Идентификатор карточки.</param>
    /// <param name="card">Новые значения полей карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><c>true</c>, если карточка найдена и обновлена; иначе <c>false</c>.</returns>
    public async Task<bool> UpdateCardAsync(int id, DashboardCard card, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато изменение карточки {CardId}.", id);

        var entity = await db.Cards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение карточки {CardId} отклонено: карточка не найдена.", id);
            return false;
        }

        card.Adapt(entity);

        // Проверим существование категории.
        var categoryExists = await db.Categories.AnyAsync(c => c.Id == entity.DashboardCategoryId, cancellationToken);

        if (!categoryExists)
        {
            logger.LogWarning("Изменение карточки {CardId} отклонено: категория {CategoryId} не найдена.", id, entity.DashboardCategoryId);
            return false;
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Карточка {CardId} обновлена.", id);
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
