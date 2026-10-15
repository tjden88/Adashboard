using Adashboard.Domain.Enums;
using Adashboard.Domain.Reference;

namespace Adashboard.Domain.Contracts;

/// <summary>
/// Контрактная модель общих настроек приложения, изменяемых пользователем через интерфейс.
/// </summary>
/// <remarks>
/// UI-модель: не содержит идентификатора хранения — настройки всегда хранятся одной записью.
/// </remarks>
public sealed class AppSettings
{
    /// <summary>Режим темы: auto, dark или light.</summary>
    public ThemeMode ThemeMode { get; set; }

    /// <summary>Признак отображения логотипа в шапке приложения.</summary>
    public bool ShowLogo { get; set; } = true;

    /// <summary>Интервал проверки статуса карточек в секундах: ноль отключает проверки.</summary>
    public int HealthCheckIntervalSeconds { get; set; } = DomainConstants.DefaultHealthCheckIntervalSeconds;
}
