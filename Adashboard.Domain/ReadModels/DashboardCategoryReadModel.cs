using Adashboard.Domain.Contracts;

namespace Adashboard.Domain.ReadModels;

/// <summary>
/// Read-модель категории dashboard: категория и её карточки. Нужна там, где карточки нельзя
/// взять из плоского списка страницы: например, при дублировании категории клиенту вместе с копией
/// категории нужны и её карточки, чтобы обновить модель страницы без перезагрузки.
/// </summary>
public sealed record DashboardCategoryReadModel
{
    /// <summary>
    /// Сама категория: при дублировании — созданная копия.
    /// </summary>
    public DashboardCategory Category { get; init; } = new();

    /// <summary>
    /// Карточки категории, упорядоченные по положению.
    /// </summary>
    public IReadOnlyList<DashboardCard> Cards { get; init; } = [];
}
