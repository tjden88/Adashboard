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

    /// <summary>Режим бокового меню страниц: скрыто, свёрнуто, автосворачивание или всегда развёрнуто.</summary>
    public SideMenuMode SideMenuMode { get; set; } = SideMenuMode.AutoCollapse;

    /// <summary>Признак отображения иконки в шапке: иконка страницы или, если у страницы своей иконки нет, иконка приложения.</summary>
    public bool ShowPageIcon { get; set; } = true;

    /// <summary>Выравнивание заголовка и описания страницы в шапке: по левому краю или по центру. На мобильной вёрстке всегда действует выравнивание по левому краю.</summary>
    public PageHeaderAlignment HeaderAlignment { get; set; } = PageHeaderAlignment.Left;

    /// <summary>Интервал проверки статуса карточек в секундах: ноль отключает проверки.</summary>
    public int HealthCheckIntervalSeconds { get; set; } = DomainConstants.DefaultHealthCheckIntervalSeconds;
}
