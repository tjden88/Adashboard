namespace Adashboard.Client.Services;

/// <summary>
/// Глобальное состояние режима редактирования dashboard.
/// </summary>
/// <remarks>
/// Владелец состояния режима: корневой элемент приложения получает от этого сервиса класс
/// <c>dashboard-edit-mode</c>. Класс включает в разметке и стилях всё, что относится
/// к редактированию (маркеры перетаскивания, кнопки категорий, оверлей карточки),
/// поэтому один источник истины покрывает и Blazor-разметку, и CSS.
/// </remarks>
public sealed class EditModeService
{
    private readonly ILogger<EditModeService> _logger;

    /// <summary>
    /// Создаёт экземпляр сервиса режима редактирования.
    /// </summary>
    /// <param name="logger">Логгер сервиса.</param>
    public EditModeService(ILogger<EditModeService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Событие изменения режима редактирования. Подписчик обязан вызвать
    /// <c>InvokeAsync(StateHasChanged)</c>, чтобы перерисовать свой компонент.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Включён ли режим редактирования.
    /// </summary>
    public bool IsEnabled
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _logger.LogInformation("Режим редактирования {State}", value ? "включён" : "выключен");
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// Переключает режим редактирования на противоположный.
    /// </summary>
    public void Toggle()
    {
        IsEnabled = !IsEnabled;
    }
}
