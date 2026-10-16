using Adashboard.Client.Helpers;
using Adashboard.Domain.Contracts;
using Microsoft.AspNetCore.Components;

namespace Adashboard.Client.Services;

/// <summary>
/// Состояние приложения, общее для всех компонентов: режим редактирования и настройки приложения.
/// </summary>
public sealed class AppStateService
{
    private readonly IDashboardApi _dashboardApi;
    private readonly ILogger<AppStateService> _logger;
    private readonly CascadingValueSource<AppStateService> _source;

    /// <summary>
    /// Создаёт состояние приложения и источник каскадного значения для него.
    /// </summary>
    public AppStateService(IDashboardApi dashboardApi, ILogger<AppStateService> logger)
    {
        _dashboardApi = dashboardApi;
        _logger = logger;
        _source = new CascadingValueSource<AppStateService>(this, isFixed: false);
    }

    /// <summary>
    /// Источник каскадного значения: регистрируется в DI, чтобы состояние доходило до компонентов.
    /// </summary>
    public CascadingValueSource<AppStateService> Source => _source;

    /// <summary>
    /// Включён ли режим редактирования dashboard.
    /// </summary>
    public bool IsEditMode { get; private set; }

    /// <summary>
    /// Настройки приложения, полученные с сервера. До загрузки отдаются значения по умолчанию.
    /// </summary>
    public AppSettings Settings { get; private set; } = new();

    /// <summary>
    /// Загружает настройки приложения из API и делает их текущими.
    /// </summary>
    public async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начата загрузка настроек.");

        try
        {
            var response = await _dashboardApi.GetSettingsAsync(cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content is null)
            {
                _logger.LogWarning("Настройки приложения не загружены. Код ответа: {StatusCode}.", (int?)response.StatusCode);
                return;
            }

            SetSettings(response.Content);
            _logger.LogDebug("Настройки приложения загружены.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Загрузка настроек завершилась ошибкой. Оставлены значения по умолчанию.");
        }
    }

    /// <summary>
    /// Сохраняет настройки приложения через API и делает их текущими. Возвращает текст ошибки,
    /// а при успехе — <c>null</c>.
    /// </summary>
    public async Task<string?> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _logger.LogDebug("Начато сохранение настроек приложения.");

        try
        {
            var response = await _dashboardApi.SaveSettingsAsync(settings, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Сохранение настроек приложения завершилось ошибкой API. Код: {StatusCode}.", (int?)response.StatusCode);
                return response.ErrorText();
            }

            SetSettings(settings);
            _logger.LogInformation("Настройки приложения сохранены.");
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Сохранение настроек приложения завершилось ошибкой.");
            return "Не удалось сохранить настройки. Проверьте соединение с сервером и попробуйте снова.";
        }
    }

    /// <summary>
    /// Переключает режим редактирования на противоположный.
    /// </summary>
    public void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        _logger.LogInformation("Режим редактирования {State}", IsEditMode ? "включён" : "выключен");

        NotifyChanged();
    }

    /// <summary>
    /// Делает указанные настройки текущими и уведомляет подписчиков.
    /// </summary>
    public void SetSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Settings = settings;
        NotifyChanged();
    }

    /// <summary>
    /// Рассылает подписчикам каскадного значения уведомление об изменении состояния.
    /// </summary>
    private void NotifyChanged()
    {
        // Уведомление уходит «в никуда»: ошибку доставки подписчикам только логируем,
        // чтобы она не всплыла как необработанное исключение задачи.
        _ = NotifyChangedSafeAsync();
    }

    private async Task NotifyChangedSafeAsync()
    {
        try
        {
            await _source.NotifyChangedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось уведомить подписчиков об изменении состояния приложения.");
        }
    }
}
