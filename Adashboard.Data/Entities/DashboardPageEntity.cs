using System.ComponentModel.DataAnnotations;

namespace Adashboard.Data.Entities;

/// <summary>
/// Сущность БД страницы dashboard.
/// </summary>
/// <remarks>
/// Ориентирована на EF Core: содержит навигацию на категории и атрибуты схемы.
/// Основная страница имеет пустой путь и не удаляется.
/// </remarks>
public sealed class DashboardPageEntity
{
    /// <summary>
    /// Уникальный идентификатор страницы.
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
    /// Относительный путь, по которому открывается страница.
    /// У основной страницы путь пустой.
    /// </summary>
    [MaxLength(120)]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Категории, входящие в страницу.
    /// </summary>
    /// <remarks>
    /// Внешний ключ связи хранится в <see cref="DashboardCategoryEntity.DashboardPageId"/>
    /// зависимой сущности (задан атрибутом на навигации <see cref="DashboardCategoryEntity.Page"/>).
    /// </remarks>
    public ICollection<DashboardCategoryEntity> Categories { get; set; } = [];
}
