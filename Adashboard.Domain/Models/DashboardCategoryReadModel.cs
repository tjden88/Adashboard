namespace Adashboard.Domain.Models;

/// <summary>
/// Узел read-модели раскладки: категория вместе с входящими в неё карточками.
/// </summary>
/// <remarks>
/// Переиспользует плоские контрактные модели <see cref="DashboardCategory"/> и
/// <see cref="DashboardCard"/>, не дублируя их поля.
/// </remarks>
public sealed class DashboardCategoryReadModel
{
    /// <summary>
    /// Данные категории без дочерних коллекций.
    /// </summary>
    public DashboardCategory Category { get; set; } = new();

    /// <summary>
    /// Карточки категории, упорядоченные по позиции.
    /// </summary>
    public IReadOnlyList<DashboardCard> Cards { get; set; } = [];
}
