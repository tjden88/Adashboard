using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Adashboard.Domain.Models;

/// <summary>
/// Модель раскладки dashboard.
/// </summary>
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
    /// <remarks>
    /// Атрибут <see cref="ForeignKeyAttribute"/> явно указывает, что внешний ключ связи
    /// хранится в свойстве <see cref="DashboardCategory.DashboardLayoutId"/> зависимой
    /// сущности (иначе EF Core определил бы связь по соглашению, создав дублирующее
    /// теневое свойство).
    /// </remarks>
    [ForeignKey(nameof(DashboardCategory.DashboardLayoutId))]
    public ICollection<DashboardCategory> Categories { get; set; } = [];
}
