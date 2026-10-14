using Adashboard.Domain.Contracts;
using Refit;

namespace Adashboard.Client.Services;

/// <summary>
/// Клиентский сервис-владелец общих настроек dashboard.
/// </summary>
/// <remarks>
/// Загружает и сохраняет настройки через API и хранит их текущий экземпляр. Компоненты
/// (шапка, карточки) читают отсюда одни и те же значения и подписываются на <see cref="OnChange"/>,
/// чтобы применить изменения сразу после сохранения, без перезагрузки страницы.
/// </remarks>
public sealed class SiteSettingsService(
    IDashboardApi dashboardApi,
    ILogger<SiteSettingsService> logger)
{
    /// <summary>
    /// Событие изменения настроек. Подписчик обновляет отображение под новые значения.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Текущие настройки dashboard. Пока они не загружены, отдаются значения по умолчанию.
    /// </summary>
    public SiteSettings Current { get; private set; } = new();

    /// <summary>
    /// Загружает настройки dashboard из API и сохраняет их текущим значением.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><c>true</c>, если настройки получены; иначе <c>false</c> (остаются прежние значения).</returns>
    public async Task<bool> LoadAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата загрузка настроек dashboard.");

        try
        {
            var settings = await dashboardApi.GetSettingsAsync(cancellationToken);
            if (settings is null)
            {
                logger.LogWarning("API не вернуло настройки dashboard. Оставлены текущие значения.");
                return false;
            }

            SetCurrent(settings);
            logger.LogDebug("Настройки dashboard загружены.");
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiException ex)
        {
            logger.LogWarning("Загрузка настроек dashboard завершилась ошибкой API. Код: {StatusCode}.", ex.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Загрузка настроек dashboard завершилась сетевой ошибкой: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Сохраняет настройки dashboard через API и делает их текущими.
    /// </summary>
    /// <param name="settings">Новые значения настроек.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <exception cref="InvalidOperationException">API вернуло ошибку или соединение не установлено.</exception>
    public async Task SaveAsync(SiteSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        logger.LogDebug("Начато сохранение настроек dashboard.");

        try
        {
            await dashboardApi.SaveSettingsAsync(settings, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiException ex)
        {
            logger.LogWarning("Сохранение настроек dashboard завершилось ошибкой API. Код: {StatusCode}.", ex.StatusCode);
            throw new InvalidOperationException($"Не удалось сохранить настройки (код {(int)ex.StatusCode}).", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Сохранение настроек dashboard завершилось сетевой ошибкой: {Message}", ex.Message);
            throw new InvalidOperationException("Не удалось подключиться к сервису.", ex);
        }

        SetCurrent(settings);
        logger.LogInformation("Настройки dashboard сохранены.");
    }

    /// <summary>
    /// Делает указанные настройки текущими и уведомляет подписчиков.
    /// </summary>
    private void SetCurrent(SiteSettings settings)
    {
        Current = settings;
        OnChange?.Invoke();
    }
}
