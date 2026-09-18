namespace Adashboard.Models.Dashboard;

/// <summary>
/// Категория ссылок на dashboard.
/// </summary>
public sealed class DashboardCategory
{
    /// <summary>
    /// Уникальный идентификатор категории.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор родительской раскладки.
    /// </summary>
    public string DashboardLayoutId { get; set; } = string.Empty;

    /// <summary>
    /// Отображаемое название категории.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Положение и ширина категории в сетке.
    /// </summary>
    public CategoryPosition Position { get; set; } = new();

    /// <summary>
    /// Карточки, входящие в категорию.
    /// </summary>
    public List<DashboardCard> Cards { get; set; } = [];
}
