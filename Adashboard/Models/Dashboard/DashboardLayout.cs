namespace Adashboard.Models.Dashboard;

/// <summary>
/// Корневая модель раскладки dashboard.
/// </summary>
public sealed class DashboardLayout
{
    /// <summary>
    /// Уникальный идентификатор раскладки.
    /// </summary>
    public string Id { get; set; } = "main";

    /// <summary>
    /// Выбранный режим темы: auto, dark или light.
    /// </summary>
    public string ThemeMode { get; set; } = "auto";

    /// <summary>
    /// Список категорий в раскладке.
    /// </summary>
    public List<DashboardCategory> Categories { get; set; } = [];
}
