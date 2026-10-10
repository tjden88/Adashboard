using Microsoft.Extensions.Logging;
using Refit;

namespace Adashboard.Services;

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
/// Проверку выполняет Adashboard.Api: браузер не может обращаться к целевым сервисам напрямую
/// из-за политики CORS (см. <c>ProbeService</c> в API). Клиент только запрашивает результат.
/// Вся сетевая работа (поиск карточки, запрос к её сервису, оценка режима <c>StatusMode</c>)
/// живёт в Adashboard.Api; клиент лишь переводит строковый статус ответа в <see cref="ServiceHealthState"/>.
/// </remarks>
public sealed class HealthCheckService(
    IDashboardApi dashboardApi,
    ILogger<HealthCheckService> logger)
{
    private const string ErrorMessage = "Не удалось проверить статус сервиса.";

    /// <summary>
    /// Выполняет одну проверку доступности карточки по её идентификатору.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат проверки: состояние и пояснение от API.</returns>
    public async Task<ServiceHealthResult> CheckAsync(int cardId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await dashboardApi.CheckHealthAsync(cardId, cancellationToken);

            if (response is null)
            {
                logger.LogWarning("API не вернуло результат проверки карточки {CardId}.", cardId);
                return ServiceHealthResult.Unknown;
            }

            return new ServiceHealthResult(ParseState(response.Status), response.Message);
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
    }

    /// <summary>
    /// Переводит строковый статус ответа API в состояние индикатора.
    /// </summary>
    private static ServiceHealthState ParseState(string? status) => status switch
    {
        "online" => ServiceHealthState.Online,
        "offline" => ServiceHealthState.Offline,
        _ => ServiceHealthState.Unknown
    };
}
