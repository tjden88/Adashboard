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
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Подзаголовок (описание), отображаемый в шапке dashboard.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Список категорий в раскладке.
    /// </summary>
    public ICollection<DashboardCategory> Categories { get; set; } = [];
}
