using Microsoft.JSInterop;

namespace Adashboard.Client.Services;

/// <summary>
/// Хранилище ключа доступа к API: держит текущий ключ в памяти и запоминает его в браузере.
/// </summary>
public sealed class ApiKeyStore(IJSRuntime js, ILogger<ApiKeyStore> logger) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <summary>
    /// Текущий ключ доступа или <c>null</c>, если он не задан.
    /// </summary>
    public string? Key { get; private set; }

    /// <summary>
    /// Событие отказа в доступе: сервер ответил 401 на запрос с текущим ключом.
    /// </summary>
    public event Func<Task>? Unauthorized;

    /// <summary>
    /// Загружает сохранённый ключ доступа из браузера.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата загрузка ключа доступа из браузера.");

        var module = await ModuleAsync(cancellationToken);
        var value = await module.InvokeAsync<string>("getApiKey", cancellationToken: cancellationToken);
        Key = string.IsNullOrWhiteSpace(value) ? null : value;

        logger.LogDebug("Ключ доступа загружен из браузера.");
    }

    /// <summary>
    /// Делает ключ доступа текущим и сохраняет его в браузере; <c>null</c> удаляет ключ.
    /// </summary>
    public async Task SetAsync(string? key, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато сохранение ключа доступа.");

        Key = string.IsNullOrWhiteSpace(key) ? null : key.Trim();

        var module = await ModuleAsync(cancellationToken);
        if (Key is null)
        {
            await module.InvokeVoidAsync("clearApiKey", cancellationToken);
        }
        else
        {
            await module.InvokeVoidAsync("setApiKey", cancellationToken, Key);
        }

        // Значение ключа в лог не пишем.
        logger.LogDebug("Ключ доступа {State}.", Key is null ? "удалён из браузера" : "сохранён в браузере");
    }

    /// <summary>
    /// Сообщает, что текущий ключ отклонён сервером.
    /// </summary>
    public Task NotifyUnauthorizedAsync() => Unauthorized?.Invoke() ?? Task.CompletedTask;

    /// <summary>
    /// Освобождает модуль хранения ключа.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (Exception)
        {
            // Соединение с JS потеряно: освобождать модуль больше не нужно.
        }
    }

    // Модуль подключаем один раз: через него ключ читается, сохраняется и удаляется.
    private async ValueTask<IJSObjectReference> ModuleAsync(CancellationToken cancellationToken)
    {
        return _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/auth.js");
    }
}
