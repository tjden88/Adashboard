using Adashboard.Data;
using Adashboard.Domain.Models;
using Adashboard.Server.Mapping;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Server.Services;

/// <summary>
/// Сервис работы с раскладкой dashboard: чтение, изменение информационной части,
/// CRUD категорий и карточек. Работает с контрактными моделями, наружу сущности не отдаёт.
/// </summary>
/// <remarks>
/// Маппинг «сущность ↔ контрактная модель» выполняет сгенерированный <see cref="DashboardMapper"/>.
/// </remarks>
public sealed class LayoutService(
    DashboardDb db,
    DashboardMapper mapper,
    ILogger<LayoutService> logger)
{
    /// <summary>
    /// Загружает основную раскладку dashboard вместе с категориями и карточками.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Контрактная модель раскладки; пустая раскладка, если записи нет.</returns>
    public async Task<DashboardLayout> GetMainLayoutAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начата загрузка основной раскладки dashboard из базы данных.");

        var entity = await db.Layouts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Categories)
            .ThenInclude(x => x.Cards)
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Основная раскладка dashboard не найдена в базе данных.");
            return new DashboardLayout { Id = 1 };
        }

        var layout = mapper.ToModel(entity);

        // Порядок из Include не гарантируется, поэтому выставляем его в памяти:
        // категории — по Order, карточки внутри категории — по Position.
        layout.Categories = layout.Categories.OrderBy(x => x.Order).ToList();
        foreach (var category in layout.Categories)
        {
            category.Cards = category.Cards.OrderBy(x => x.Position).ToList();
        }

        logger.LogInformation("Основная раскладка dashboard загружена: категорий {CategoryCount}.", layout.Categories.Count);
        return layout;
    }

    /// <summary>
    /// Изменяет заголовок и описание основной раскладки.
    /// </summary>
    /// <param name="info">Новые заголовок и описание.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><c>true</c>, если раскладка найдена и обновлена; иначе <c>false</c>.</returns>
    public async Task<bool> UpdateInfoAsync(DashboardLayoutInfo info, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато изменение информационной части раскладки dashboard.");

        var entity = await db.Layouts.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение информационной части отклонено: раскладка dashboard не найдена.");
            return false;
        }

        entity.Title = info.Title;
        entity.Description = info.Description;
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
    public async Task<DashboardCategory> CreateCategoryAsync(DashboardCategory category, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато создание категории {Title}.", category.Title);

        var entity = mapper.ToEntity(category);
        // Идентификатор назначает EF Core, а категория всегда принадлежит основной раскладке.
        entity.Id = 0;
        entity.DashboardLayoutId = 1;

        db.Categories.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Категория создана. Идентификатор: {CategoryId}.", entity.Id);
        return mapper.ToModel(entity);
    }

    /// <summary>
    /// Изменяет существующую категорию.
    /// </summary>
    /// <param name="id">Идентификатор категории.</param>
    /// <param name="category">Новые значения полей категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><c>true</c>, если категория найдена и обновлена; иначе <c>false</c>.</returns>
    public async Task<bool> UpdateCategoryAsync(int id, DashboardCategory category, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато изменение категории {CategoryId}.", id);

        var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение категории {CategoryId} отклонено: категория не найдена.", id);
            return false;
        }

        mapper.UpdateCategory(category, entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Категория {CategoryId} обновлена.", id);
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
        logger.LogInformation("Начато удаление категории {CategoryId}.", id);

        var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Удаление категории {CategoryId} отклонено: категория не найдена.", id);
            return false;
        }

        // Карточки категории удаляются каскадно на уровне БД (обязательная связь).
        db.Categories.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

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
        logger.LogInformation("Начато создание карточки {Title} в категории {CategoryId}.", card.Title, card.DashboardCategoryId);

        var categoryExists = await db.Categories.AnyAsync(x => x.Id == card.DashboardCategoryId, cancellationToken);
        if (!categoryExists)
        {
            logger.LogWarning("Создание карточки отклонено: категория {CategoryId} не найдена.", card.DashboardCategoryId);
            return null;
        }

        var entity = mapper.ToEntity(card);
        entity.Id = 0;

        db.Cards.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Карточка создана. Идентификатор: {CardId}.", entity.Id);
        return mapper.ToModel(entity);
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
        logger.LogInformation("Начато изменение карточки {CardId}.", id);

        var entity = await db.Cards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Изменение карточки {CardId} отклонено: карточка не найдена.", id);
            return false;
        }

        mapper.UpdateCard(card, entity);

        // Перенос в другую категорию выполняем только при явно указанном идентификаторе,
        // иначе сохраняем текущую категорию карточки.
        if (card.DashboardCategoryId > 0)
        {
            entity.DashboardCategoryId = card.DashboardCategoryId;
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
        logger.LogInformation("Начато удаление карточки {CardId}.", id);

        var entity = await db.Cards.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Удаление карточки {CardId} отклонено: карточка не найдена.", id);
            return false;
        }

        db.Cards.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Карточка {CardId} удалена.", id);
        return true;
    }
}
