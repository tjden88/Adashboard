namespace Adashboard.Models.Dashboard;

/// <summary>
/// Карточка ссылки внутри категории.
/// </summary>
public sealed class DashboardCard
{
    /// <summary>
    /// Уникальный идентификатор карточки.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор категории, к которой принадлежит карточка.
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Отображаемое название карточки.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Адрес перехода по карточке.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// CSS-класс иконки Font Awesome.
    /// </summary>
    public string IconClass { get; set; } = string.Empty;

    /// <summary>
    /// Цвет иконки в формате CSS (например, #22c55e).
    /// </summary>
    public string IconColor { get; set; } = "#fb923c";

    /// <summary>
    /// Признак доступности сервиса.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Положение карточки внутри категории.
    /// </summary>
    public CardPosition Position { get; set; } = new();
}
