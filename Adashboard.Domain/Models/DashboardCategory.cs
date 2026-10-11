using System.ComponentModel.DataAnnotations;

namespace Adashboard.Domain.Models;

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
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Порядок категории среди остальных.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Ширина категории в колонках сетки.
    /// </summary>
    public int Width { get; set; } = 6;

    /// <summary>
    /// Карточки, входящие в категорию.
    /// </summary>
    public ICollection<DashboardCard> Cards { get; set; } = [];
}
