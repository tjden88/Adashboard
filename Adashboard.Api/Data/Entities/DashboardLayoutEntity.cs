using System.ComponentModel.DataAnnotations;

namespace Adashboard.Api.Data.Entities;

/// <summary>
/// Сущность БД раскладки dashboard.
/// </summary>
/// <remarks>
/// Ориентирована на EF Core: содержит навигацию на категории и атрибуты схемы.
/// Идентификатор раскладки фиксирован (основная раскладка — строка <c>Id = 1</c>).
/// </remarks>
public sealed class DashboardLayoutEntity
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
    /// Категории, входящие в раскладку.
    /// </summary>
    /// <remarks>
    /// Внешний ключ связи хранится в <see cref="DashboardCategoryEntity.DashboardLayoutId"/>
    /// зависимой сущности (задан атрибутом на навигации <see cref="DashboardCategoryEntity.Layout"/>).
    /// </remarks>
    public ICollection<DashboardCategoryEntity> Categories { get; set; } = [];
}
