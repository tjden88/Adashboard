using Adashboard.Domain.Contracts;

namespace Adashboard.Domain.ReadModels;

/// <summary>
/// Read-модель раскладки dashboard: агрегат, который отдаёт <c>GET /api/layout</c>.
/// </summary>
/// <remarks>
/// Переиспользует плоские контрактные модели: саму раскладку <see cref="DashboardLayout"/>
/// и её категории. Вложенность строится в сервисе, а не маппингом полного графа сущностей.
/// </remarks>
public sealed class DashboardLayoutReadModel
{
    /// <summary>
    /// Информационная часть раскладки (идентификатор, заголовок и описание).
    /// </summary>
    public DashboardLayout Layout { get; set; } = new();

    /// <summary>
    /// Категории раскладки вместе с их карточками, упорядоченные для отображения.
    /// </summary>
    public IReadOnlyList<DashboardCategoryReadModel> Categories { get; set; } = [];
}
