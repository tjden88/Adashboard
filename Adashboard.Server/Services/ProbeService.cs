using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Adashboard.Data;
using Adashboard.Domain.Dto;
using Adashboard.Domain.Extensions;
using Adashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Server.Services;


/// <summary>
/// Сервис серверной проверки карточек: доступность сервисов и заголовки страниц.
/// </summary>
/// <remarks>
/// <para>
/// Запросы выполняются из backend, а не из браузера, поэтому политика CORS целевых сервисов
/// не мешает проверке статуса и получению заголовка.
/// </para>
/// <para>
/// Фильтрация диапазонов адресов (защита от SSRF) здесь намеренно <b>не применяется</b>: карточки
/// ведут на локальные и частные домашние сервисы, и доступ к ним — смысл дашборда. Это сознательный
/// отказ от защиты, обратный логике <c>ImageIconService.EnsurePublicHostAsync</c>. Остаются
/// ограничения: только схемы http/https, без следования редиректам, ограниченные таймауты и размер
/// вычитываемого тела.
/// </para>
/// </remarks>
public sealed class ProbeService(
    DashboardDb db,
    IHttpClientFactory httpClientFactory,
    ILogger<ProbeService> logger)
{
    private const string HttpClientName = "Probe";
    private const int MaxHtmlBytes = 256 * 1024;
    private const int MaxTitleLength = 120;

    private static readonly TimeSpan HealthRequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TitleRequestTimeout = TimeSpan.FromSeconds(8);

    // Захватываем содержимое тега title без учёта регистра и переносов строк.
    private static readonly Regex TitleRegex = new(
        @"<title[^>]*>(?<title>.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Проверяет доступность сервиса карточки по её идентификатору.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат проверки или <c>null</c>, если карточка не найдена.</returns>
    public async Task<HealthCheckStatus?> CheckCardAsync(int cardId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начата проверка доступности карточки {CardId}.", cardId);

        var card = await db.Cards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            logger.LogWarning("Проверка доступности карточки {CardId} пропущена: карточка не найдена.", cardId);
            return null;
        }

        // Режим «Не проверять»: запрос не делаем, сообщаем клиенту нейтральное состояние.
        if (card.StatusMode == CardStatusMode.None)
        {
            logger.LogDebug("Проверка доступности карточки {CardId} не требуется: режим проверки отключён.", cardId);
            return new HealthCheckStatus(HealthCheckStatus.HealthStatus.Unknown, "Проверка статуса отключена.");
        }

        if (!TryBuildAbsoluteUri(card.UrlScheme.ToSchemeText() + card.Url, out var uri))
        {
            logger.LogWarning(
                "Проверка доступности карточки {CardId} пропущена: адрес не является абсолютным HTTP(S)-адресом.",
                cardId);
            return new HealthCheckStatus(HealthCheckStatus.HealthStatus.Unknown, "Некорректный адрес карточки.");
        }

        logger.LogDebug("Проверка доступности карточки {CardId} на хосте {Host}.", cardId, uri.Host);

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);

            // Таймаут через отдельный токен: так его можно отличить от отмены всего цикла проверок.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(HealthRequestTimeout);

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var result = Evaluate(card.StatusMode, (int)response.StatusCode);

            if (result.Status == HealthCheckStatus.HealthStatus.Offline)
            {
                logger.LogWarning(
                    "Сервис карточки {CardId} недоступен. Код ответа: {StatusCode}.",
                    cardId,
                    (int)response.StatusCode);
            }
            else
            {
                logger.LogInformation("Проверка доступности карточки {CardId} завершена: {Status}.", cardId, result.Status);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Отмена всего цикла проверок: пробрасываем, чтобы корректно завершить работу.
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Проверка доступности карточки {CardId} не завершилась за отведённое время.", cardId);
            return new HealthCheckStatus(
                HealthCheckStatus.HealthStatus.Offline,
                $"Превышено время ожидания ({HealthRequestTimeout.TotalSeconds:0} с).");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                "Проверка доступности карточки {CardId} завершилась сетевой ошибкой: {Message}",
                cardId,
                ex.Message);

            var message = ex.HttpRequestError == HttpRequestError.NameResolutionError
                ? "Не удалось разрешить адрес сервиса."
                : "Не удалось подключиться к сервису.";

            return new HealthCheckStatus(HealthCheckStatus.HealthStatus.Offline, message);
        }
    }

    /// <summary>
    /// Пытается получить заголовок HTML-страницы по указанному адресу.
    /// </summary>
    /// <param name="url">Абсолютная HTTP(S)-ссылка на страницу.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Заголовок страницы или <c>null</c>, если он не найден либо страница недоступна.</returns>
    public async Task<string?> GetTitleAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!TryBuildAbsoluteUri(url, out var uri))
        {
            logger.LogWarning("Получение заголовка отклонено: адрес не является абсолютным HTTP(S)-адресом.");
            throw new InvalidOperationException("Укажите корректную HTTP(S)-ссылку.");
        }

        logger.LogInformation("Начато получение заголовка страницы на хосте {Host}.", uri.Host);

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TitleRequestTimeout);

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Получение заголовка завершилось неудачно. Код ответа: {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            var content = await ReadLimitedAsync(response.Content, cancellationToken);
            var html = DecodeHtml(content, response.Content.Headers.ContentType?.CharSet);
            var title = ExtractTitle(html);

            if (title is null)
            {
                logger.LogWarning("На странице хоста {Host} не найден тег title.", uri.Host);
                return null;
            }

            logger.LogInformation("Заголовок страницы на хосте {Host} успешно получен.", uri.Host);
            return title;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Получение заголовка не завершилось за отведённое время.");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Получение заголовка завершилось сетевой ошибкой: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Определяет состояние сервиса по режиму проверки и полученному коду ответа.
    /// </summary>
    private static HealthCheckStatus Evaluate(CardStatusMode mode, int statusCode)
    {
        var online = mode switch
        {
            CardStatusMode.Success => statusCode is >= 200 and < 300,
            CardStatusMode.NoServerError => statusCode < 500,
            CardStatusMode.Reachable => true,
            _ => false
        };

        return online
            ? new HealthCheckStatus(HealthCheckStatus.HealthStatus.Online, $"Доступен ({statusCode})")
            : new HealthCheckStatus(HealthCheckStatus.HealthStatus.Offline, $"Сервер вернул ошибку: {statusCode}.");
    }

    /// <summary>
    /// Читает ответ с ограничением размера, чтобы не вычитывать в память большой документ.
    /// </summary>
    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        while (buffer.Length < MaxHtmlBytes)
        {
            var remaining = MaxHtmlBytes - (int)buffer.Length;
            var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, remaining)), cancellationToken);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Декодирует HTML с учётом кодировки из заголовка ответа, по умолчанию — UTF-8.
    /// </summary>
    private static string DecodeHtml(byte[] bytes, string? charset)
    {
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset.Trim().Trim('"', '\'')).GetString(bytes);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                // Неизвестная или недоступная кодировка: используем UTF-8.
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Извлекает и нормализует заголовок из HTML.
    /// </summary>
    private static string? ExtractTitle(string html)
    {
        var match = TitleRegex.Match(html);
        if (!match.Success)
        {
            return null;
        }

        var title = WebUtility.HtmlDecode(match.Groups["title"].Value);
        title = WhitespaceRegex.Replace(title, " ").Trim();

        if (title.Length == 0)
        {
            return null;
        }

        return title.Length > MaxTitleLength ? title[..MaxTitleLength] : title;
    }

    /// <summary>
    /// Пытается собрать абсолютный HTTP(S)-адрес из строки ссылки.
    /// </summary>
    private static bool TryBuildAbsoluteUri(string url, out Uri uri)
    {
        uri = null!;

        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || parsed.Host.Length == 0)
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
