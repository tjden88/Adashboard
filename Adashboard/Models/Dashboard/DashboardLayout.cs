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
    /// Список категорий в раскладке.
    /// </summary>
    public List<DashboardCategory> Categories { get; set; } = [];
}
