using Adashboard.Domain.Contracts;
using Microsoft.AspNetCore.Components;

namespace Adashboard.Client.Services;

/// <summary>
/// Состояние приложения, общее для всех компонентов: режим редактирования и настройки dashboard.
/// </summary>
public class AppState
{
    private readonly ILogger<AppState> _logger;
    private readonly CascadingValueSource<AppState> _source;

    /// <summary>
    /// Создаёт состояние приложения и источник каскадного значения для него.
    /// </summary>
    public AppState(ILogger<AppState> logger)
    {
        _logger = logger;
        _source = new CascadingValueSource<AppState>(this, isFixed: false);
    }

    /// <summary>
    /// Источник каскадного значения: регистрируется в DI, чтобы состояние доходило до компонентов.
    /// </summary>
    public CascadingValueSource<AppState> Source => _source;

    /// <summary>
    /// Включён ли режим редактирования dashboard.
    /// </summary>
    public bool IsEditMode { get; private set; }

    /// <summary>
    /// Настройки dashboard, полученные с сервера. До загрузки отдаются значения по умолчанию.
    /// </summary>
    public SiteSettings Settings { get; private set; } = new();

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
    /// <param name="settings">Настройки dashboard, полученные с сервера.</param>
    public void SetSettings(SiteSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Settings = settings;
        NotifyChanged();
    }

    /// <summary>
    /// Рассылает подписчикам каскадного значения уведомление об изменении состояния.
    /// </summary>
    /// <remarks>
    /// <c>NotifyChangedAsync</c> только ставит уведомление в очередь рендерера и возвращает
    /// управление сразу, поэтому в синхронном методе его результат можно не ожидать.
    /// </remarks>
    private void NotifyChanged() => _ = _source.NotifyChangedAsync();
}
