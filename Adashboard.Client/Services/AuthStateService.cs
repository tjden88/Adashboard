using System.Net;
using Microsoft.AspNetCore.Components;

namespace Adashboard.Client.Services;

/// <summary>
/// Состояние доступа клиента к API: подтверждён ли ключ доступа и нужно ли показать экран входа.
/// </summary>
public sealed class AuthStateService : IDisposable
{
    private readonly IDashboardApi _dashboardApi;
    private readonly ApiKeyStore _store;
    private readonly ILogger<AuthStateService> _logger;
    private readonly CascadingValueSource<AuthStateService> _source;

    /// <summary>
    /// Создаёт состояние доступа и источник каскадного значения для него.
    /// </summary>
    public AuthStateService(IDashboardApi dashboardApi, ApiKeyStore store, ILogger<AuthStateService> logger)
    {
        _dashboardApi = dashboardApi;
        _store = store;
        _logger = logger;
        _source = new CascadingValueSource<AuthStateService>(this, isFixed: false);
        _store.Unauthorized += OnUnauthorizedAsync;
    }

    /// <summary>
    /// Источник каскадного значения: регистрируется в DI, чтобы состояние доходило до компонентов.
    /// </summary>
    public CascadingValueSource<AuthStateService> Source => _source;

    /// <summary>
    /// Проверка доступа завершена: можно решать, показывать dashboard или экран ввода ключа.
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// Доступ к API подтверждён.
    /// </summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// В браузере сохранён ключ доступа: пользователь может сменить его.
    /// </summary>
    public bool HasKey => !string.IsNullOrEmpty(_store.Key);

    /// <summary>
    /// Проверяет сохранённый ключ доступа при запуске приложения.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начата проверка доступа к API.");

        await _store.LoadAsync(cancellationToken);
        var access = await ProbeAsync(cancellationToken);

        if (access == ApiAccess.Unauthorized)
        {
            _logger.LogWarning("Доступ к API не подтверждён: сохранённый ключ отсутствует или отклонён.");
            await _store.SetAsync(null, cancellationToken);
        }

        IsAuthenticated = access != ApiAccess.Unauthorized;
        IsReady = true;
        _logger.LogDebug("Проверка доступа завершена: {State}.", IsAuthenticated ? "доступ разрешён" : "требуется ключ доступа");
        NotifyChanged();
    }

    /// <summary>
    /// Проверяет введённый ключ и при успехе делает его текущим. Возвращает текст ошибки,
    /// а при успехе — <c>null</c>.
    /// </summary>
    public async Task<string?> SignInAsync(string key, CancellationToken cancellationToken = default)
    {
        var isEmpty = string.IsNullOrWhiteSpace(key);

        _logger.LogDebug("Начата проверка введённого ключа доступа.");
        await _store.SetAsync(isEmpty ? null : key, cancellationToken);

        var access = await ProbeAsync(cancellationToken);
        if (access == ApiAccess.Granted)
        {
            _logger.LogInformation("Доступ к API подтверждён.");
            IsAuthenticated = true;
            IsReady = true;
            NotifyChanged();
            return null;
        }

        // Неподтверждённый ключ не сохраняем: иначе он уходил бы в запросы как действующий.
        await _store.SetAsync(null, cancellationToken);

        if (access == ApiAccess.Unauthorized)
        {
            _logger.LogWarning("Введённый ключ доступа отклонён сервером.");
            return isEmpty
                ? "Введите ключ доступа."
                : "Ключ доступа не подошёл. Проверьте значение и попробуйте снова.";
        }

        _logger.LogWarning("Проверка ключа доступа не удалась: сервер не ответил.");
        return "Не удалось проверить ключ доступа. Проверьте соединение с сервером и попробуйте снова.";
    }

    /// <summary>
    /// Удаляет сохранённый ключ доступа и возвращает приложение к экрану входа.
    /// </summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато удаление ключа доступа.");
        await _store.SetAsync(null, cancellationToken);

        IsAuthenticated = false;
        _logger.LogInformation("Ключ доступа удалён: выполнен выход.");
        NotifyChanged();
    }

    /// <summary>
    /// Отписывается от уведомлений хранилища ключа.
    /// </summary>
    public void Dispose() => _store.Unauthorized -= OnUnauthorizedAsync;

    // Проверяет доступ, запрашивая служебный эндпоинт API.
    private async Task<ApiAccess> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _dashboardApi.GetHealthAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ApiAccess.Granted;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ApiAccess.Unauthorized;
            }

            // Любой другой отказ (например, нет связи) не говорит о неверном ключе: даём
            // приложению загрузиться и показать ошибку на странице, а не экран входа.
            _logger.LogWarning("Проверка доступа завершилась ответом {StatusCode}.", (int?)response.StatusCode);
            return ApiAccess.Unknown;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Проверка доступа завершилась ошибкой.");
            return ApiAccess.Unknown;
        }
    }

    // Сервер отклонил текущий ключ (ответ 401): сбрасываем вход, чтобы пользователь ввёл ключ заново.
    private async Task OnUnauthorizedAsync()
    {
        if (!IsAuthenticated)
        {
            return;
        }

        _logger.LogWarning("Сервер отклонил ключ доступа: требуется повторный вход.");
        await _store.SetAsync(null);

        IsAuthenticated = false;
        NotifyChanged();
    }

    // Рассылает подписчикам каскадного значения уведомление об изменении состояния.
    private void NotifyChanged()
    {
        // Уведомление уходит «в никуда»: ошибку доставки подписчикам только логируем,
        // чтобы она не всплыла как необработанное исключение задачи.
        _ = NotifyChangedSafeAsync();
    }

    private async Task NotifyChangedSafeAsync()
    {
        try
        {
            await _source.NotifyChangedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось уведомить подписчиков об изменении доступа.");
        }
    }

    // Результат проверки доступа к API.
    private enum ApiAccess
    {
        /// <summary>
        /// Доступ подтверждён: сервер принял запрос.
        /// </summary>
        Granted,

        /// <summary>
        /// Сервер требует ключ доступа, и он не подошёл.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Проверить доступ не удалось: например, нет связи с сервером.
        /// </summary>
        Unknown
    }
}
