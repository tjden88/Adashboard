using Adashboard.Models.Dashboard;

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
/// Проверяет доступность сервисов, на которые ведут карточки dashboard.
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Выполняет одну проверку доступности карточки согласно её режиму <see cref="DashboardCard.StatusMode"/>.
    /// </summary>
    /// <param name="card">Карточка, статус которой нужно проверить.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Состояние доступности сервиса.</returns>
    Task<ServiceHealthState> CheckAsync(DashboardCard card, CancellationToken cancellationToken = default);
}

/// <summary>
/// Реализация <see cref="IHealthCheckService"/> через HTTP-запрос к адресу карточки.
/// </summary>
/// <remarks>
/// Проверки домашних сервисов подразумевают обращение к локальным и частным адресам,
/// поэтому, в отличие от <see cref="ImageIconService"/>, здесь ограничение SSRF намеренно не применяется.
/// </remarks>
public sealed class HealthCheckService(
    IHttpClientFactory httpClientFactory,
    ILogger<HealthCheckService> logger) : IHealthCheckService
{
    private const string HttpClientName = "HealthCheck";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public async Task<ServiceHealthState> CheckAsync(DashboardCard card, CancellationToken cancellationToken = default)
    {
        if (card.StatusMode == CardStatusMode.None)
        {
            return ServiceHealthState.Unknown;
        }

        if (!TryBuildAbsoluteUri(card, out var uri))
        {
            logger.LogWarning(
                "Проверка статуса карточки {CardId} пропущена: адрес не является абсолютным HTTP(S)-адресом.",
                card.Id);
            return ServiceHealthState.Unknown;
        }

        logger.LogDebug("Начата проверка статуса карточки {CardId} на хосте {Host}.", card.Id, uri.Host);

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);

            // Таймаут задаём через отдельный токен, чтобы отличить его от отмены цикла проверок.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(RequestTimeout);

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var state = Evaluate(card.StatusMode, response.StatusCode);

            if (state == ServiceHealthState.Offline)
            {
                logger.LogWarning(
                    "Сервис карточки {CardId} недоступен. Код ответа: {StatusCode}.",
                    card.Id,
                    (int)response.StatusCode);
            }
            else
            {
                logger.LogDebug("Проверка статуса карточки {CardId} завершена: {State}.", card.Id, state);
            }

            return state;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Отмена всего цикла проверок: пробрасываем, чтобы корректно завершить работу.
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Проверка статуса карточки {CardId} не завершилась за отведённое время.", card.Id);
            return ServiceHealthState.Offline;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                "Проверка статуса карточки {CardId} завершилась сетевой ошибкой: {Message}",
                card.Id,
                ex.Message);
            return ServiceHealthState.Offline;
        }
    }

    /// <summary>
    /// Определяет состояние сервиса по режиму проверки и полученному коду ответа.
    /// </summary>
    private static ServiceHealthState Evaluate(CardStatusMode mode, System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;

        return mode switch
        {
            CardStatusMode.Success => code is >= 200 and < 300 ? ServiceHealthState.Online : ServiceHealthState.Offline,
            CardStatusMode.NoServerError => code < 500 ? ServiceHealthState.Online : ServiceHealthState.Offline,
            CardStatusMode.Reachable => ServiceHealthState.Online,
            _ => ServiceHealthState.Unknown
        };
    }

    /// <summary>
    /// Пытается собрать абсолютный HTTP(S)-адрес карточки из схемы и адреса.
    /// </summary>
    private static bool TryBuildAbsoluteUri(DashboardCard card, out Uri uri)
    {
        uri = null!;

        var value = card.UrlScheme + card.Url;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (parsed.Host.Length == 0)
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
