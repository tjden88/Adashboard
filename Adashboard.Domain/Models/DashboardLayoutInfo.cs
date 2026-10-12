using System.ComponentModel.DataAnnotations;

namespace Adashboard.Domain.Models;

/// <summary>
/// Тело запроса на изменение информационной части раскладки dashboard (заголовок и описание).
/// </summary>
public sealed class DashboardLayoutInfo
{
    /// <summary>
    /// Заголовок, отображаемый в шапке dashboard.
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Подзаголовок (описание), отображаемый в шапке dashboard.
    /// </summary>
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;
}
