namespace Adashboard.Models.Dashboard;

/// <summary>
/// Значения настроек dashboard, передаваемые из диалога настроек.
/// </summary>
/// <param name="ThemeMode">Режим темы: auto, dark или light.</param>
/// <param name="SiteTitle">Заголовок dashboard (может быть пустым намеренно).</param>
/// <param name="SiteDescription">Описание dashboard (может быть пустым намеренно).</param>
/// <param name="ShowLogo">Признак отображения логотипа в шапке dashboard.</param>
public sealed record SiteSettings(string ThemeMode, string SiteTitle, string SiteDescription, bool ShowLogo);
