using Adashboard.Data;
using Adashboard.Domain.Models;
using Adashboard.Server.Mapping;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Server.Services;

/// <summary>
/// Сервис чтения и обновления общих настроек dashboard.
/// </summary>
/// <remarks>
/// Настройки хранятся одной строкой (<c>Id = 1</c>) и всегда возвращаются контрактной моделью.
/// Маппинг «сущность ↔ контрактная модель» выполняет <see cref="DashboardMapper"/>.
/// </remarks>
public sealed class SiteSettingsService(
    DashboardDb db,
    DashboardMapper mapper,
    ILogger<SiteSettingsService> logger)
{
    /// <summary>
    /// Возвращает текущие настройки dashboard.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Настройки; значения по умолчанию, если записи в базе нет.</returns>
    public async Task<SiteSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато чтение настроек dashboard.");

        var entity = await db.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Настройки dashboard не найдены в базе данных. Возвращены значения по умолчанию.");
            return new SiteSettings();
        }

        logger.LogInformation("Настройки dashboard прочитаны.");
        return mapper.ToModel(entity);
    }

    /// <summary>
    /// Обновляет настройки dashboard. Если записи нет, она создаётся.
    /// </summary>
    /// <param name="settings">Новые значения настроек.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public async Task UpdateAsync(SiteSettings settings, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато обновление настроек dashboard.");

        var entity = await db.SiteSettings.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Настройки dashboard не найдены. Создаётся новая запись.");
            entity = mapper.ToEntity(settings);
            entity.Id = 1;
            db.SiteSettings.Add(entity);
        }
        else
        {
            mapper.UpdateSettings(settings, entity);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Настройки dashboard обновлены.");
    }
}
