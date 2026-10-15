using Adashboard.Data;
using Adashboard.Data.Entities;
using Adashboard.Domain.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using WPR.Operations;

namespace Adashboard.Server.Services;

/// <summary>
/// Сервис чтения и обновления общих настроек dashboard.
/// </summary>
public sealed class SiteSettingsService(DashboardDb db, ILogger<SiteSettingsService> logger)
{
    /// <summary>
    /// Возвращает текущие настройки dashboard.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Настройки; значения по умолчанию, если записи в базе нет.</returns>
    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато чтение настроек dashboard.");

        var entity = await db.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Настройки dashboard не найдены в базе данных. Возвращены значения по умолчанию.");
            return new AppSettings();
        }

        logger.LogDebug("Настройки dashboard прочитаны.");
        return entity.Adapt<AppSettings>();
    }

    /// <summary>
    /// Обновляет настройки dashboard. Если записи нет, она создаётся.
    /// </summary>
    public async Task<OperationResult> UpdateAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато обновление настроек dashboard.");

        if (settings.HealthCheckIntervalSeconds < 0)
        {
            logger.LogWarning("Обновление настроек отклонено: некорректный интервал проверки статуса.");
            return Result.Failure("Интервал проверки статуса должен быть больше либо равен нулю.");
        }

        var entity = await db.SiteSettings.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Настройки dashboard не найдены. Создаётся новая запись.");
            entity = settings.Adapt<SiteSettingsEntity>();
            entity.Id = 1;
            db.SiteSettings.Add(entity);
        }
        else
        {
            settings.Adapt(entity);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Настройки dashboard обновлены.");
        return Result.Success();
    }
}
