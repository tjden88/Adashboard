namespace Adashboard.Models.Dashboard;

/// <summary>
/// Карточка ссылки внутри категории.
/// </summary>
public sealed class DashboardCard
{
    /// <summary>
    /// Уникальный идентификатор карточки.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор категории, к которой принадлежит карточка.
    /// </summary>
    public int CategoryId { get; set; }

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
    /// Признак широкой карточки: занимает две колонки вместо одной.
    /// </summary>
    public bool IsWide { get; set; }

    /// <summary>
    /// Признак доступности сервиса.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Положение карточки внутри категории.
    /// </summary>
    public CardPosition Position { get; set; } = new();
}
