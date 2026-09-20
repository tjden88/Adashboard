using Microsoft.Extensions.Logging;

namespace Adashboard.Services;

/// <summary>
/// Глобальное состояние режима редактирования dashboard.
/// </summary>
public sealed class EditModeService
{
    private readonly ILogger<EditModeService> _logger;

    private bool _isEnabled;

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
        get => _isEnabled;
        private set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
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
