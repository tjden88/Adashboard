namespace Adashboard.Domain.Models;

/// <summary>
/// Общие настройки dashboard, которые могут быть изменены пользователем через интерфейс.
/// </summary>
public sealed class SiteSettings
{
    /// <summary>Режим темы: auto, dark или light.</summary>
    public ThemeMode ThemeMode { get; set; }

    /// <summary>Признак отображения логотипа в шапке dashboard.</summary>
    public bool ShowLogo { get; init; } = true;

    /// <summary>Интервал проверки статуса карточек в секундах.</summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;
}