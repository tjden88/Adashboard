using Microsoft.AspNetCore.Components;

namespace Adashboard.Client.Services;

/// <summary>
/// Основа диалога: типизированные параметры, закрытие окна с результатом и признак занятости.
/// </summary>
public abstract class DialogComponent<TParameters, TResult> : ComponentBase
{
    /// <summary>
    /// Параметры диалога, заданные в месте открытия.
    /// </summary>
    [Parameter, EditorRequired]
    public TParameters Parameters { get; set; } = default!;

    /// <summary>
    /// Окно диалога: через него диалог закрывает себя и сообщает о занятости; задаёт контейнер.
    /// </summary>
    [Parameter]
    public DialogInstance<TResult> Dialog { get; set; } = null!;

    /// <summary>
    /// Закрывает окно с результатом работы диалога.
    /// </summary>
    protected void Close(TResult result) => Dialog.Close(result);

    /// <summary>
    /// Помечает диалог занятым (идёт сохранение): окно блокирует крестик и Esc.
    /// </summary>
    protected void SetBusy(bool isBusy) => Dialog.SetBusy(isBusy);
}
