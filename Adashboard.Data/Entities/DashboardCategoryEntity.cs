using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Adashboard.Data.Entities;

/// <summary>
/// Сущность БД категории ссылок.
/// </summary>
/// <remarks>
/// Ориентирована на EF Core: двусторонние навигации (родительская раскладка и дочерние
/// карточки) и атрибуты схемы.
/// </remarks>
public sealed class DashboardCategoryEntity
{
    /// <summary>
    /// Уникальный идентификатор категории.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор родительской раскладки.
    /// </summary>
    public int DashboardLayoutId { get; set; }

    /// <summary>
    /// Отображаемое название категории.
    /// </summary>
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Порядок категории среди остальных.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Ширина категории в колонках сетки.
    /// </summary>
    public int Width { get; set; } = 6;

    /// <summary>
    /// Родительская раскладка (навигация).
    /// </summary>
    [ForeignKey(nameof(DashboardLayoutId))]
    public DashboardLayoutEntity? Layout { get; set; }

    /// <summary>
    /// Карточки, входящие в категорию.
    /// </summary>
    /// <remarks>
    /// Внешний ключ связи хранится в <see cref="DashboardCardEntity.DashboardCategoryId"/>
    /// зависимой сущности (задан атрибутом на навигации <see cref="DashboardCardEntity.Category"/>).
    /// Карточки удаляются каскадно вместе с категорией.
    /// </remarks>
    public ICollection<DashboardCardEntity> Cards { get; set; } = [];
}
