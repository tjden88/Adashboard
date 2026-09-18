namespace Adashboard.Models.Dashboard;

/// <summary>
/// Позиция категории в сетке dashboard.
/// </summary>
public sealed class CategoryPosition
{
    /// <summary>
    /// Порядок категории среди остальных.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Ширина категории в колонках сетки.
    /// </summary>
    public int Width { get; set; } = 4;
}
