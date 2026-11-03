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

    /// <summary>
    /// CSS-класс иконки Font Awesome, отображаемой в шапке перед заголовком.
    /// Пустая строка означает отсутствие иконки.
    /// </summary>
    [MaxLength(DomainConstants.PageIconClassMaxLength)]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Цвет иконки страницы в формате CSS (например, #ffffff).
    /// Пустая строка — цвет, задаваемый темой.
    /// </summary>
    [MaxLength(DomainConstants.PageIconColorMaxLength)]
    public string IconColor { get; set; } = string.Empty;

    /// <summary>
    /// Порядок страницы в боковом меню. У основной страницы всегда 0: она идёт первой
    /// и не участвует в перестановке, остальные страницы занимают позиции от 1.
    /// </summary>
    public int Order { get; set; }
}
