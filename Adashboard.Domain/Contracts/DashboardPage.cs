using System.ComponentModel.DataAnnotations;
using Adashboard.Domain.ReadModels;
using Adashboard.Domain.Reference;

namespace Adashboard.Domain.Contracts;

/// <summary>
/// Контрактная модель страницы dashboard: отдельная страница с собственным адресом,
/// категориями и карточками.
/// </summary>
/// <remarks>
/// Плоская модель без дочерних коллекций: содержит только собственные поля страницы.
/// Агрегат для чтения (категории с карточками) отдаёт отдельная read-модель
/// <see cref="DashboardPageReadModel"/>.
/// Основная страница имеет пустой <see cref="Path"/> и удалить её нельзя.
/// </remarks>
public sealed class DashboardPage
{
    /// <summary>
    /// Уникальный идентификатор страницы.
    /// В теле запроса не используется: страница определяется адресом эндпоинта.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Заголовок, отображаемый в шапке dashboard.
    /// </summary>
    [Required]
    [MaxLength(DomainConstants.PageTitleMaxLength)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Подзаголовок (описание), отображаемый в шапке dashboard.
    /// </summary>
    [MaxLength(DomainConstants.PageDescriptionMaxLength)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Относительный путь, по которому открывается страница.
    /// У основной страницы путь пустой.
    /// </summary>
    [MaxLength(DomainConstants.PagePathMaxLength)]
    public string Path { get; set; } = string.Empty;
}
