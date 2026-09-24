namespace Adashboard.Models.Dashboard;

/// <summary>
/// Корневая модель раскладки dashboard.
/// </summary>
public sealed class DashboardLayout
{
    /// <summary>
    /// Уникальный идентификатор раскладки.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Выбранный режим темы: auto, dark или light.
    /// </summary>
    public string ThemeMode { get; set; } = "auto";

    /// <summary>
    /// Заголовок, отображаемый в шапке dashboard.
    /// </summary>
    public string SiteTitle { get; set; } = "Adashboard";

    /// <summary>
    /// Подзаголовок (описание), отображаемый в шапке dashboard.
    /// </summary>
    public string SiteDescription { get; set; } = "Личная стартовая страница со ссылками и сервисами";

    /// <summary>
    /// Список категорий в раскладке.
    /// </summary>
    public List<DashboardCategory> Categories { get; set; } = [];
}
