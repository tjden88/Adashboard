using Adashboard.Domain.Contracts;
using Microsoft.AspNetCore.Components;

namespace Adashboard.Client.Services;

/// <summary>
/// Состояние приложения, общее для всех компонентов: режим редактирования и настройки dashboard.
/// </summary>
public class AppStateService
{
    private readonly ILogger<AppStateService> _logger;
    private readonly CascadingValueSource<AppStateService> _source;

    /// <summary>
    /// Создаёт состояние приложения и источник каскадного значения для него.
    /// </summary>
    public AppStateService(ILogger<AppStateService> logger)
    {
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
    private void NotifyChanged() => _ = _source.NotifyChangedAsync();
}
