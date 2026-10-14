namespace Adashboard.Client.Services;

/// <summary>
/// Размер окна диалога.
/// </summary>
public enum DialogSize
{
    /// <summary>
    /// Узкое окно: короткие подтверждения и вопросы.
    /// </summary>
    Small,

    /// <summary>
    /// Обычное окно: размер по умолчанию.
    /// </summary>
    Medium,

    /// <summary>
    /// Широкое окно: формы с несколькими полями и списками.
    /// </summary>
    Large
}

/// <summary>
/// Настройки открытия диалога.
/// </summary>
public sealed record DialogOptions(DialogSize Size = DialogSize.Medium, bool CloseOnOverlayClick = false)
{
    /// <summary>
    /// Настройки по умолчанию: обычный размер окна, клик вне окна диалог не закрывает.
    /// </summary>
    public static DialogOptions Default { get; } = new();
}
