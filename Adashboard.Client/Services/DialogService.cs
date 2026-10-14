namespace Adashboard.Client.Services;

/// <summary>
/// Управляет стопкой диалоговых окон: открывает окно с типизированными параметрами
/// и отдаёт результат его работы.
/// </summary>
public sealed class DialogService
{
    private readonly ILogger<DialogService> _logger;
    private readonly List<DialogInstance> _dialogs = [];

    /// <summary>
    /// Создаёт сервис диалоговых окон.
    /// </summary>
    public DialogService(ILogger<DialogService> logger) => _logger = logger;

    /// <summary>
    /// Событие изменения стопки диалогов: по нему контейнер перерисовывает окна.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Открытые диалоги в порядке открытия: последний элемент — верхний (активный).
    /// </summary>
    public IReadOnlyList<DialogInstance> Dialogs => _dialogs;

    /// <summary>
    /// Верхний (активный) диалог или <c>null</c>, если окон нет.
    /// </summary>
    public DialogInstance? Top => _dialogs.Count > 0 ? _dialogs[^1] : null;

    /// <summary>
    /// Открывает диалог и возвращает результат его работы. Если окно закрыли без результата
    /// (Esc, крестик, клик вне окна), результатом становится значение по умолчанию.
    /// </summary>
    public Task<TResult> ShowAsync<TDialog, TParameters, TResult>(TParameters parameters, string title, DialogOptions? options = null)
        where TDialog : DialogComponent<TParameters, TResult>
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _logger.LogDebug("Начато открытие диалога {Dialog}.", typeof(TDialog).Name);

        var instance = new DialogInstance<TResult>(typeof(TDialog), title, options ?? DialogOptions.Default, parameters, RemoveDialog);
        instance.Changed += OnDialogStateChanged;

        _dialogs.Add(instance);
        OnChange?.Invoke();

        _logger.LogInformation("Диалог {Dialog} открыт. Открытых окон: {Count}.", typeof(TDialog).Name, _dialogs.Count);

        return instance.Result;
    }

    /// <summary>
    /// Закрывает диалог без результата. Окно, занятое длительной операцией, не закрывается.
    /// </summary>
    public void Cancel(DialogInstance dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (dialog.IsBusy)
        {
            _logger.LogWarning("Закрытие диалога {Dialog} отклонено: идёт длительная операция.", dialog.ComponentType.Name);
            return;
        }

        _logger.LogDebug("Отмена диалога {Dialog}.", dialog.ComponentType.Name);
        dialog.Cancel();
    }

    private void OnDialogStateChanged() => OnChange?.Invoke();

    private void RemoveDialog(DialogInstance dialog)
    {
        dialog.Changed -= OnDialogStateChanged;

        if (!_dialogs.Remove(dialog))
        {
            return;
        }

        _logger.LogInformation("Диалог {Dialog} закрыт. Открытых окон: {Count}.", dialog.ComponentType.Name, _dialogs.Count);
        OnChange?.Invoke();
    }
}
