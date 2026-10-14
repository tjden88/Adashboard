using Adashboard.Domain.Contracts;

namespace Adashboard.Domain.ReadModels;

/// <summary>
/// Read-модель страницы dashboard: агрегат, который отдаёт <c>GET /api/pages/{id}</c>.
/// </summary>
/// <remarks>
/// Переиспользует плоские контрактные модели: саму страницу <see cref="DashboardPage"/>
/// и её категории. Вложенность строится в сервисе, а не маппингом полного графа сущностей.
/// Модель неизменяема: заполняется один раз при чтении и дальше только сериализуется.
/// </remarks>
public sealed record DashboardPageReadModel
{
    /// <summary>
    /// Информационная часть страницы (идентификатор, заголовок, описание и путь).
    /// </summary>
    public DashboardPage Page { get; init; } = new();

    /// <summary>
    /// Категории страницы
    /// </summary>
    public IReadOnlyList<DashboardCategory> Categories { get; init; } = [];

    /// <summary>
    /// Карточки категорий
    /// </summary>
    public IReadOnlyList<DashboardCard> Cards { get; init; } = [];
}
