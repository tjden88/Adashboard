namespace Adashboard.Client.Components.Dialogs;

/// <summary>
/// Вид основной кнопки диалога подтверждения.
/// </summary>
public enum ConfirmDialogAction
{
    /// <summary>
    /// Опасное действие: красная кнопка с иконкой корзины.
    /// </summary>
    Danger,

    /// <summary>
    /// Обычное действие: основная кнопка с иконкой подтверждения.
    /// </summary>
    Primary
}

/// <summary>
/// Параметры диалога подтверждения.
/// </summary>
public sealed record ConfirmDialogParameters(string Message, string ConfirmLabel = "Удалить", ConfirmDialogAction Action = ConfirmDialogAction.Danger);
