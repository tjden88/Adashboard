namespace Adashboard.Domain;

/// <summary>
/// Позиция карточки внутри категории.
/// </summary>
public sealed class CardPosition
{
    /// <summary>
    /// Порядок карточки в списке категории.
    /// </summary>
    public int Order { get; set; }
}
