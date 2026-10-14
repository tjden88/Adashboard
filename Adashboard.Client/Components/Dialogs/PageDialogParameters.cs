namespace Adashboard.Client.Components.Dialogs;

/// <summary>
/// Параметры диалога создания страницы dashboard.
/// </summary>
public sealed record PageDialogParameters(IReadOnlyList<string> ExistingPaths);
