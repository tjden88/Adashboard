using Adashboard.Domain.Contracts;

namespace Adashboard.Domain.ReadModels;

/// <summary>
/// Read-модель раскладки dashboard: агрегат, который отдаёт <c>GET /api/layout</c>.
/// </summary>
/// <remarks>
/// Переиспользует плоские контрактные модели: саму раскладку <see cref="DashboardLayout"/>
/// и её категории. Вложенность строится в сервисе, а не маппингом полного графа сущностей.
/// Модель неизменяема: заполняется один раз при чтении и дальше только сериализуется.
/// </remarks>
public sealed record DashboardLayoutReadModel
{
    /// <summary>
    /// Информационная часть раскладки (идентификатор, заголовок и описание).
    /// </summary>
    public DashboardLayout Layout { get; init; } = new();

    /// <summary>
    /// Категории раскладки вместе с их карточками, упорядоченные для отображения.
    /// </summary>
    public IReadOnlyList<DashboardCategoryReadModel> Categories { get; init; } = [];
}
