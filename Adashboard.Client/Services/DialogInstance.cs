namespace Adashboard.Client.Services;

/// <summary>
/// Описание открытого диалога. Необобщённый базовый тип нужен контейнеру, который
/// отрисовывает стопку окон; результат работы хранит обобщённый наследник.
/// </summary>
public abstract class DialogInstance
{
    private readonly Action<DialogInstance> _onClosed;

    /// <summary>
    /// Создаёт описание открытого диалога.
    /// </summary>
    protected DialogInstance(Type componentType, string title, DialogOptions options, object parameters, Action<DialogInstance> onClosed)
    {
        ComponentType = componentType;
        Title = title;
        Options = options;
        Parameters = parameters;
        _onClosed = onClosed;
    }

    /// <summary>
    /// Идентификатор окна: ключ отрисовки в стопке и адрес элемента окна для работы с фокусом.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Тип компонента, отображаемого в окне.
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Заголовок окна.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Настройки окна.
    /// </summary>
    public DialogOptions Options { get; }

    /// <summary>
    /// Параметры компонента диалога.
    /// </summary>
    public object Parameters { get; }

    /// <summary>
    /// Идёт длительная операция (например, сохранение): окно блокирует крестик и Esc.
    /// </summary>
    public bool IsBusy { get; private set; }

    /// <summary>
    /// Событие изменения состояния окна: по нему контейнер перерисовывает диалоги.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Помечает окно занятым или свободным.
    /// </summary>
    public void SetBusy(bool isBusy)
    {
        if (IsBusy == isBusy)
        {
            return;
        }

        IsBusy = isBusy;
        Changed?.Invoke();
    }

    /// <summary>
    /// Закрывает окно без результата: используется отменой (Esc, крестик, клик вне окна).
    /// </summary>
    internal abstract void Cancel();

    /// <summary>
    /// Сообщает сервису, что окно закрыто и его нужно убрать из стопки.
    /// </summary>
    protected void NotifyClosed() => _onClosed(this);
}

/// <summary>
/// Открытый диалог с типизированным результатом.
/// </summary>
public sealed class DialogInstance<TResult> : DialogInstance
{
    private readonly TaskCompletionSource<TResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal DialogInstance(Type componentType, string title, DialogOptions options, object parameters, Action<DialogInstance> onClosed)
        : base(componentType, title, options, parameters, onClosed)
    {
    }

    /// <summary>
    /// Результат работы окна: завершается при закрытии диалога.
    /// </summary>
    public Task<TResult> Result => _completion.Task;

    /// <summary>
    /// Закрывает окно с результатом.
    /// </summary>
    public void Close(TResult result)
    {
        if (_completion.TrySetResult(result))
        {
            NotifyClosed();
        }
    }

    internal override void Cancel() => Close(default!);
}
