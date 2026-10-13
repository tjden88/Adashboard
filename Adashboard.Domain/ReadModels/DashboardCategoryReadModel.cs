using Adashboard.Domain.Contracts;

namespace Adashboard.Domain.ReadModels;

/// <summary>
/// Узел read-модели раскладки: категория вместе с входящими в неё карточками.
/// </summary>
/// <remarks>
/// Переиспользует плоские контрактные модели <see cref="DashboardCategory"/> и
/// <see cref="DashboardCard"/>, не дублируя их поля. Модель неизменяема:
/// заполняется один раз при чтении и дальше только сериализуется.
/// </remarks>
public sealed record DashboardCategoryReadModel
{
    /// <summary>
    /// Данные категории без дочерних коллекций.
    /// </summary>
    public DashboardCategory Category { get; init; } = new();

    /// <summary>
    /// Карточки категории, упорядоченные по позиции.
    /// </summary>
    public IReadOnlyList<DashboardCard> Cards { get; init; } = [];
}
