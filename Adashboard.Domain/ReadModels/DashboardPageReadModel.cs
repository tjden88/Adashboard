using Adashboard.Domain.Contracts;

namespace Adashboard.Domain.ReadModels;

/// <summary>
/// Read-модель страницы dashboard: агрегат страницы с её категориями и карточками,
/// который отдаёт <c>GET /api/pages/{id}</c> и полный снапшот <c>GET /api/pages/full</c>.
/// </summary>
/// <remarks>
/// Переиспользует плоские контрактные модели: саму страницу <see cref="DashboardPage"/>
/// и её категории. Вложенность строится в сервисе, а не маппингом полного графа сущностей.
/// Модель изменяемая: клиентский сервис хранит полученные экземпляры и мутирует её списки
/// после изменений, не перечитывая страницу из API.
/// </remarks>
public sealed record DashboardPageReadModel
{
    /// <summary>
    /// Информационная часть страницы (идентификатор, заголовок, описание и путь).
    /// </summary>
    public DashboardPage Page { get; set; } = new();

    /// <summary>
    /// Категории страницы
    /// </summary>
    public List<DashboardCategory> Categories { get; set; } = [];

    /// <summary>
    /// Карточки категорий
    /// </summary>
    public List<DashboardCard> Cards { get; set; } = [];
}
