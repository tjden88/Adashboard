using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Adashboard.Services;

/// <summary>
/// Сервис для управления диалоговыми окнами.
/// </summary>
public sealed class DialogService
{
    private readonly ILogger<DialogService> _logger;

    /// <summary>
    /// Создаёт экземпляр сервиса диалоговых окон.
    /// </summary>
    /// <param name="logger">Логгер сервиса.</param>
    public DialogService(ILogger<DialogService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Событие, возникающее при изменении текущего диалога.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Текущий открытый диалог или <c>null</c>, если диалог не открыт.
    /// </summary>
    public DialogRequest? Current { get; private set; }

    /// <summary>
    /// Открывает диалоговое окно с указанным компонентом.
    /// </summary>
    /// <typeparam name="TComponent">Тип компонента диалога.</typeparam>
    /// <param name="title">Заголовок диалога.</param>
    /// <param name="parameters">Параметры компонента диалога.</param>
    /// <param name="closeOnOverlayClick">Закрывать ли диалог при клике вне его области.</param>
    public void Show<TComponent>(
        string? title = null,
        Dictionary<string, object?>? parameters = null,
        bool closeOnOverlayClick = true)
        where TComponent : IComponent
    {
        Show(typeof(TComponent), title, parameters, closeOnOverlayClick);
    }

    /// <summary>
    /// Открывает диалоговое окно с указанным типом компонента.
    /// </summary>
    /// <param name="componentType">Тип компонента диалога.</param>
    /// <param name="title">Заголовок диалога.</param>
    /// <param name="parameters">Параметры компонента диалога.</param>
    /// <param name="closeOnOverlayClick">Закрывать ли диалог при клике вне его области.</param>
    public void Show(
        Type componentType,
        string? title = null,
        Dictionary<string, object?>? parameters = null,
        bool closeOnOverlayClick = true)
    {
        ArgumentNullException.ThrowIfNull(componentType);

        if (Current is not null)
        {
            _logger.LogWarning("Диалог {PreviousComponent} заменён диалогом {Component}", Current.ComponentType.Name, componentType.Name);
        }

        _logger.LogInformation("Открытие диалога {Component}", componentType.Name);

        Current = new DialogRequest(componentType, title, parameters, closeOnOverlayClick);
        OnChange?.Invoke();

        _logger.LogInformation("Диалог {Component} открыт", componentType.Name);
    }

    /// <summary>
    /// Закрывает текущий диалог.
    /// </summary>
    public void Close()
    {
        if (Current is null)
        {
            _logger.LogWarning("Запрошено закрытие диалога, но активный диалог отсутствует");
            return;
        }

        _logger.LogInformation("Закрытие диалога {Component}", Current.ComponentType.Name);

        var componentName = Current.ComponentType.Name;
        Current = null;
        OnChange?.Invoke();

        _logger.LogInformation("Диалог {Component} закрыт", componentName);
    }
}

/// <summary>
/// Описание открытого диалогового окна.
/// </summary>
/// <param name="ComponentType">Тип компонента, который отображается в диалоге.</param>
/// <param name="Title">Заголовок диалога.</param>
/// <param name="Parameters">Параметры компонента диалога.</param>
/// <param name="CloseOnOverlayClick">Закрывать ли диалог при клике вне его области.</param>
public record DialogRequest(
    Type ComponentType,
    string? Title,
    Dictionary<string, object?>? Parameters,
    bool CloseOnOverlayClick);
