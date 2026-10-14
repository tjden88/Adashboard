using Adashboard.Domain.Enums;
using Refit;

namespace Adashboard.Client.Services;

/// <summary>
/// Состояние доступности сервиса карточки, определённое последней проверкой.
/// </summary>
public enum ServiceHealthState
{
    /// <summary>Проверка ещё не выполнялась или невозможна.</summary>
    Unknown,

    /// <summary>Сервис доступен согласно выбранному режиму проверки.</summary>
    Online,

    /// <summary>Сервис недоступен или не ответил.</summary>
    Offline
}

/// <summary>
/// Результат одной проверки доступности: состояние и пояснение для подсказки на карточке.
/// </summary>
/// <param name="State">Состояние доступности сервиса.</param>
/// <param name="Message">Пояснение исхода от API или <c>null</c>, если пояснения нет.</param>
public sealed record ServiceHealthResult(ServiceHealthState State, string? Message)
{
    /// <summary>
    /// Нейтральный результат по умолчанию: состояние неизвестно, пояснения нет.
    /// </summary>
    public static ServiceHealthResult Unknown { get; } = new(ServiceHealthState.Unknown, null);
}

/// <summary>
/// Сервис проверки доступности сервисов, на которые ведут карточки dashboard.
/// </summary>
/// <remarks>
/// Проверку выполняет Adashboard.Server: браузер не может обращаться к целевым сервисам напрямую
/// из-за политики CORS (см. <c>ProbeService</c> в API). Клиент только запрашивает результат.
/// Вся сетевая работа (поиск карточки, запрос к её сервису, оценка режима <c>StatusMode</c>)
/// живёт в Adashboard.Server; клиент лишь переводит состояние сервиса из ответа в <see cref="ServiceHealthState"/>.
/// </remarks>
public sealed class HealthCheckService(
    IDashboardApi dashboardApi,
    ILogger<HealthCheckService> logger)
{
    private const string ErrorMessage = "Не удалось проверить статус сервиса.";

    /// <summary>Максимальное число одновременных проверок доступности.</summary>
    private const int MaxConcurrentChecks = 6;

    /// <summary>
    /// Общий ограничитель параллельных проверок внутри одного circuit.
    /// </summary>
    /// <remarks>
    /// Каждая карточка проверяет себя сама и независимо от остальных, поэтому без ограничения
    /// десятки карточек начали бы опрашивать сервер одновременно. Семафор сдерживает поток
    /// запросов, чтобы не перегружать сеть и API.
    /// </remarks>
    private readonly SemaphoreSlim _concurrencyLimiter = new(MaxConcurrentChecks, MaxConcurrentChecks);

    /// <summary>
    /// Выполняет одну проверку доступности карточки по её идентификатору.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат проверки: состояние и пояснение от API.</returns>
    public async Task<ServiceHealthResult> CheckAsync(int cardId, CancellationToken cancellationToken = default)
    {
        // Занимаем «слот» до начала запроса: проверок одновременно не больше MaxConcurrentChecks.
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            var response = await dashboardApi.CheckHealthAsync(cardId, cancellationToken);

            if (response is null)
            {
                logger.LogWarning("API не вернуло результат проверки карточки {CardId}.", cardId);
                return ServiceHealthResult.Unknown;
            }

            return new ServiceHealthResult(MapState(response.Status), response.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Отмена всего цикла проверок: пробрасываем, чтобы корректно завершить работу.
            throw;
        }
        catch (ApiException ex)
        {
            logger.LogWarning(
                "Проверка карточки {CardId} завершилась ошибкой API. Код: {StatusCode}.",
                cardId,
                ex.StatusCode);
            return new ServiceHealthResult(ServiceHealthState.Unknown, ErrorMessage);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Проверка карточки {CardId} завершилась сетевой ошибкой: {Message}", cardId, ex.Message);
            return new ServiceHealthResult(ServiceHealthState.Unknown, ErrorMessage);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    /// <summary>
    /// Переводит состояние сервиса из ответа API в состояние индикатора на клиенте.
    /// </summary>
    private static ServiceHealthState MapState(HealthStatus status) => status switch
    {
        HealthStatus.Online => ServiceHealthState.Online,
        HealthStatus.Offline => ServiceHealthState.Offline,
        _ => ServiceHealthState.Unknown
    };
}
