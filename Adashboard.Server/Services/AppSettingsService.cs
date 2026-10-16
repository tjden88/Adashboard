using Adashboard.Data;
using Adashboard.Data.Entities;
using Adashboard.Domain.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using WPR.Operations;

namespace Adashboard.Server.Services;

/// <summary>
/// Сервис чтения и обновления общих настроек приложения.
/// </summary>
public sealed class AppSettingsService(DashboardDb db, ILogger<AppSettingsService> logger)
{
    /// <summary>
    /// Возвращает текущие настройки приложения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Настройки; значения по умолчанию, если записи в базе нет.</returns>
    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато чтение настроек приложения.");

        var entity = await db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Настройки приложения не найдены в базе данных. Возвращены значения по умолчанию.");
            return new AppSettings();
        }

        logger.LogDebug("Настройки приложения прочитаны.");
        return entity.Adapt<AppSettings>();
    }

    /// <summary>
    /// Обновляет настройки приложения. Если записи нет, она создаётся.
    /// </summary>
    public async Task<OperationResult> UpdateAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато обновление настроек приложения.");

        if (settings.HealthCheckIntervalSeconds < 0)
        {
            logger.LogWarning("Обновление настроек отклонено: некорректный интервал проверки статуса.");
            return Result.Failure("Интервал проверки статуса должен быть больше либо равен нулю.");
        }

        var entity = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("Настройки приложения не найдены. Создаётся новая запись.");
            entity = settings.Adapt<AppSettingsEntity>();
            entity.Id = 1;
            db.AppSettings.Add(entity);
        }
        else
        {
            settings.Adapt(entity);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Настройки приложения обновлены.");
        return Result.Success();
    }
}
