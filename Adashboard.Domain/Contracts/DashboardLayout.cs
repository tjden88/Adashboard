using System.ComponentModel.DataAnnotations;
using Adashboard.Domain.ReadModels;

namespace Adashboard.Domain.Contracts;

/// <summary>
/// Контрактная модель раскладки dashboard для записи: тело <c>PUT /api/layout/info</c>.
/// </summary>
/// <remarks>
/// Плоская модель без дочерних коллекций: содержит только собственные поля раскладки.
/// Агрегат для чтения (категории с карточками) отдаёт отдельная read-модель
/// <see cref="DashboardLayoutReadModel"/>.
/// </remarks>
public sealed class DashboardLayout
{
    /// <summary>
    /// Уникальный идентификатор раскладки.
    /// В теле запроса не используется: раскладка определяется адресом эндпоинта.
    /// </summary>
    public int Id { get; set; }

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
