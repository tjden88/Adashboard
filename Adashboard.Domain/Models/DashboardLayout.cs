using System.ComponentModel.DataAnnotations;

namespace Adashboard.Domain.Models;

/// <summary>
/// Контрактная модель раскладки dashboard: то, что получает фронтенд на <c>GET /api/layout</c>.
/// </summary>
/// <remarks>
/// UI-модель: содержит категории с карточками, но не имеет навигаций и атрибутов схемы БД.
/// </remarks>
public sealed class DashboardLayout
{
    /// <summary>
    /// Уникальный идентификатор раскладки.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Заголовок, отображаемый в шапке dashboard.
    /// </summary>
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Подзаголовок (описание), отображаемый в шапке dashboard.
    /// </summary>
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Список категорий в раскладке.
    /// </summary>
    public ICollection<DashboardCategory> Categories { get; set; } = [];
}
