namespace Adashboard.Models.Dashboard;

/// <summary>
/// Категория ссылок на dashboard.
/// </summary>
public sealed class DashboardCategory
{
    /// <summary>
    /// Уникальный идентификатор категории.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор родительской раскладки.
    /// </summary>
    public int DashboardLayoutId { get; set; }

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
