using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Adashboard.Data;
using Adashboard.Domain.Enums;
using Adashboard.Domain.Extensions;
using Adashboard.Domain.ReadModels;
using Adashboard.Domain.Reference;
using Adashboard.Server.Helpers;
using Microsoft.EntityFrameworkCore;
using WPR.Operations;

namespace Adashboard.Server.Services;


/// <summary>
/// Сервис серверной проверки карточек: доступность сервисов, заголовки и иконки страниц.
/// </summary>
public sealed class ProbeService(DashboardDb db, HttpClient client, ILogger<ProbeService> logger)
{
    private const int MaxHtmlBytes = 256 * 1024;

    // Сколько раз вручную следуем перенаправлению при загрузке страницы или иконки.
    private const int MaxRedirects = 3;

    private static readonly TimeSpan _HealthRequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _TitleRequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan _IconRequestTimeout = TimeSpan.FromSeconds(10);

    // Захватываем содержимое тега title без учёта регистра и переносов строк.
    private static readonly Regex _TitleRegex = new(@"<title[^>]*>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex _WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    // Теги meta и link разбираем по атрибутам: их порядок и кавычки на страницах произвольные.
    private static readonly Regex _MetaTagRegex = new(@"<meta\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _LinkTagRegex = new(@"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _AttributeRegex = new(@"([-\w:]+)\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<uq>[^\s>]+))", RegexOptions.Compiled);

    /// <summary>
    /// Проверяет доступность сервиса карточки по её идентификатору.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Результат проверки либо ошибка «карточка не найдена».</returns>
    public async Task<OperationResult<HealthCheckResult>> CheckCardAsync(int cardId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начата проверка доступности карточки {CardId}.", cardId);

        var card = await db.Cards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            logger.LogWarning("Проверка доступности карточки {CardId} пропущена: карточка не найдена.", cardId);
            return Result.Failure<HealthCheckResult>("Карточка не найдена.");
        }

        // Режим «Не проверять»: запрос не делаем, сообщаем клиенту нейтральное состояние.
        if (card.StatusMode == CardStatusMode.None)
        {
            logger.LogDebug("Проверка доступности карточки {CardId} не требуется: режим проверки отключён.", cardId);
            return new HealthCheckResult(HealthStatus.Unknown, "Проверка статуса отключена.");
        }

        if (!TryBuildAbsoluteUri(card.UrlScheme.ToSchemeText() + card.Url, out var uri))
        {
            logger.LogWarning(
                "Проверка доступности карточки {CardId} пропущена: адрес не является абсолютным HTTP(S)-адресом.",
                cardId);
            return new HealthCheckResult(HealthStatus.Unknown, "Некорректный адрес карточки.");
        }

        logger.LogDebug("Проверка доступности карточки {CardId} на хосте {Host}.", cardId, uri.Host);

        try
        {

            // Таймаут через отдельный токен: так его можно отличить от отмены всего цикла проверок.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_HealthRequestTimeout);

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var result = Evaluate(card.StatusMode, (int)response.StatusCode);

            if (result.Status == HealthStatus.Offline)
            {
                logger.LogWarning("Сервис карточки {CardId} недоступен. Код ответа: {StatusCode}.", cardId, (int)response.StatusCode);
            }
            else
            {
                logger.LogDebug("Проверка доступности карточки {CardId} завершена: {Status}.", cardId, result.Status);
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
            return new HealthCheckResult(HealthStatus.Offline, $"Превышено время ожидания ({_HealthRequestTimeout.TotalSeconds:0} с).");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Проверка доступности карточки {CardId} завершилась сетевой ошибкой: {Message}", cardId, ex.Message);

            var message = ex.HttpRequestError == HttpRequestError.NameResolutionError
                ? "Не удалось разрешить адрес сервиса."
                : "Не удалось подключиться к сервису.";

            return new HealthCheckResult(HealthStatus.Offline, message);
        }
    }

    /// <summary>
    /// Пытается получить заголовок HTML-страницы по указанному адресу.
    /// </summary>
    /// <param name="url">Абсолютная HTTP(S)-ссылка на страницу.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Заголовок страницы, <c>null</c> если он не найден либо страница недоступна, или ошибка некорректной ссылки.</returns>
    public async Task<OperationResult<string?>> GetTitleAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!TryBuildAbsoluteUri(url, out var uri))
        {
            logger.LogWarning("Получение заголовка отклонено: адрес не является абсолютным HTTP(S)-адресом.");
            return Result.Failure("Укажите корректную HTTP(S)-ссылку.").As<string?>();
        }

        logger.LogDebug("Начато получение заголовка страницы на хосте {Host}.", uri.Host);

        try
        {

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_TitleRequestTimeout);

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Получение заголовка завершилось неудачно. Код ответа: {StatusCode}.", (int)response.StatusCode); return null;
            }

            var content = await ReadLimitedAsync(response.Content, MaxHtmlBytes, timeoutCts.Token);
            var html = DecodeHtml(content, response.Content.Headers.ContentType?.CharSet);
            var title = ExtractTitle(html);

            if (title is null)
            {
                logger.LogWarning("На странице хоста {Host} не найден тег title.", uri.Host);
                return Result.Failure("Заголовок не найден на странице").As<string?>();
            }

            logger.LogDebug("Заголовок страницы на хосте {Host} успешно получен.", uri.Host);
            return title;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Получение заголовка не завершилось за отведённое время.");
            return Result.Failure("Время запроса истекло").As<string?>();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Получение заголовка завершилось сетевой ошибкой: {Message}", ex.Message);
            return Result.Failure($"Ошибка ответа: {ex.StatusCode}").As<string?>();
        }
    }

    /// <summary>
    /// Пытается получить иконку сервиса: сначала изображение со страницы (og:image),
    /// затем favicon сайта. Изображение возвращается как data URL.
    /// </summary>
    public async Task<OperationResult<string>> GetIconAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!TryBuildAbsoluteUri(url, out var pageUri))
        {
            logger.LogWarning("Получение иконки отклонено: адрес не является абсолютным HTTP(S)-адресом.");
            return Result.Failure("Укажите корректную HTTP(S)-ссылку.").As<string>();
        }

        logger.LogDebug("Начато получение иконки сервиса на хосте {Host}.", pageUri.Host);

        try
        {
            var html = await DownloadHtmlAsync(pageUri, cancellationToken);
            if (html is null)
            {
                logger.LogWarning("Получение иконки не удалось: страница хоста {Host} недоступна.", pageUri.Host);
                return Result.Failure("Не удалось загрузить страницу сервиса.").As<string>();
            }

            // Идём по кандидатам по приоритету: первый, который скачался и подошёл по формату, побеждает.
            foreach (var candidate in BuildIconCandidates(html, pageUri))
            {
                var icon = await TryDownloadIconAsync(candidate, cancellationToken);
                if (icon is null)
                {
                    continue;
                }

                logger.LogDebug("Иконка сервиса на хосте {Host} получена.", pageUri.Host);
                return $"data:{icon.Value.ContentType};base64,{Convert.ToBase64String(icon.Value.Content)}";
            }

            logger.LogWarning("На странице хоста {Host} не найдена подходящая иконка.", pageUri.Host);
            return Result.Failure("Не удалось найти подходящую иконку на странице.").As<string>();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Получение иконки не завершилось за отведённое время.");
            return Result.Failure("Время запроса истекло.").As<string>();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Получение иконки завершилось сетевой ошибкой: {Message}", ex.Message);
            return Result.Failure("Не удалось загрузить иконку.").As<string>();
        }
    }

    /// <summary>
    /// Определяет состояние сервиса по режиму проверки и полученному коду ответа.
    /// </summary>
    private static HealthCheckResult Evaluate(CardStatusMode mode, int statusCode)
    {
        var online = mode switch
        {
            CardStatusMode.Success => statusCode is >= 200 and < 300,
            CardStatusMode.NoServerError => statusCode < 500,
            CardStatusMode.Reachable => true,
            _ => false
        };

        return online
            ? new HealthCheckResult(HealthStatus.Online, $"Доступен ({statusCode})")
            : new HealthCheckResult(HealthStatus.Offline, $"Сервер вернул ошибку: {statusCode}.");
    }

    /// <summary>
    /// Скачивает HTML страницы, из которого берутся кандидаты на иконку.
    /// </summary>
    private async Task<string?> DownloadHtmlAsync(Uri pageUri, CancellationToken cancellationToken)
    {
        var downloaded = await DownloadLimitedAsync(pageUri, MaxHtmlBytes, cancellationToken);
        if (downloaded is null)
        {
            logger.LogWarning("Загрузка страницы {Uri} для поиска иконки не удалась.", pageUri);
            return null;
        }

        return DecodeHtml(downloaded.Value.Content, downloaded.Value.Charset);
    }

    /// <summary>
    /// Скачивает изображение-кандидат и определяет его формат.
    /// </summary>
    private async Task<(string ContentType, byte[] Content)?> TryDownloadIconAsync(Uri uri, CancellationToken cancellationToken)
    {
        // Читаем на байт больше предела, чтобы отличить «ровно предел» от «больше предела».
        var downloaded = await DownloadLimitedAsync(uri, (int)DomainConstants.MaxImageBytes + 1, cancellationToken);
        if (downloaded is null)
        {
            return null;
        }

        var content = downloaded.Value.Content;
        if (content.Length == 0 || content.LongLength > DomainConstants.MaxImageBytes)
        {
            logger.LogDebug("Иконка {Uri} пропущена: недопустимый размер.", uri);
            return null;
        }

        var extension = ImageSignature.DetectExtension(content);
        var contentType = extension is null ? null : ImageSignature.GetContentType(extension);
        if (contentType is null)
        {
            logger.LogDebug("Иконка {Uri} пропущена: неподдерживаемый формат.", uri);
            return null;
        }

        return (contentType, content);
    }

    /// <summary>
    /// Скачивает содержимое по адресу, вручную следуя перенаправлениям: общий HttpClient
    /// редиректы не следует, а у страниц и иконок они часты (http → https, переход на CDN).
    /// </summary>
    private async Task<(byte[] Content, string? Charset)?> DownloadLimitedAsync(Uri uri, int maxBytes, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_IconRequestTimeout);

        try
        {
            var current = uri;

            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                using var response = await client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (!TryResolveRedirect(current, response.Headers.Location, out current))
                    {
                        logger.LogDebug("Загрузка {Uri} прервана: перенаправление не разрешено.", uri);
                        return null;
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogDebug("Загрузка {Uri} прервана: код ответа {StatusCode}.", uri, (int)response.StatusCode);
                    return null;
                }

                var content = await ReadLimitedAsync(response.Content, maxBytes, timeoutCts.Token);
                return (content, response.Content.Headers.ContentType?.CharSet);
            }

            logger.LogDebug("Загрузка {Uri} прервана: слишком много перенаправлений.", uri);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            logger.LogDebug("Загрузка {Uri} прервана: {Message}", uri, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Собирает адреса-кандидаты на иконку в порядке приоритета: изображение со страницы,
    /// затем favicon. Относительные адреса приводятся к абсолютным относительно страницы.
    /// </summary>
    private static List<Uri> BuildIconCandidates(string html, Uri pageUri)
    {
        // Приоритет: 0 — og:image, 1 — twitter:image, 2 — link rel="icon",
        // 3 — apple-touch-icon, 4 — favicon.ico в корне сайта.
        var found = new List<(int Priority, string Value)>();

        foreach (Match meta in _MetaTagRegex.Matches(html))
        {
            var attributes = ReadAttributes(meta.Value);
            if (!attributes.TryGetValue("content", out var content) || string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            attributes.TryGetValue("property", out var property);
            attributes.TryGetValue("name", out var name);
            var key = property ?? name;

            if (string.Equals(key, "og:image", StringComparison.OrdinalIgnoreCase))
            {
                found.Add((0, content));
            }
            else if (string.Equals(key, "twitter:image", StringComparison.OrdinalIgnoreCase))
            {
                found.Add((1, content));
            }
        }

        foreach (Match link in _LinkTagRegex.Matches(html))
        {
            var attributes = ReadAttributes(link.Value);
            if (!attributes.TryGetValue("rel", out var rel) || !attributes.TryGetValue("href", out var href) || string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var tokens = rel.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Any(t => string.Equals(t, "apple-touch-icon", StringComparison.OrdinalIgnoreCase)))
            {
                found.Add((3, href));
            }
            else if (tokens.Any(t => string.Equals(t, "icon", StringComparison.OrdinalIgnoreCase)))
            {
                found.Add((2, href));
            }
        }

        found.Add((4, "/favicon.ico"));

        var candidates = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, value) in found.OrderBy(x => x.Priority))
        {
            if (!TryResolveUri(pageUri, value, out var resolved) || !seen.Add(resolved.AbsoluteUri))
            {
                continue;
            }

            candidates.Add(resolved);
        }

        return candidates;
    }

    /// <summary>
    /// Читает атрибуты тега HTML в словарь без учёта регистра имён.
    /// </summary>
    private static Dictionary<string, string> ReadAttributes(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in _AttributeRegex.Matches(tag))
        {
            var value = match.Groups["dq"].Success ? match.Groups["dq"].Value
                : match.Groups["sq"].Success ? match.Groups["sq"].Value
                : match.Groups["uq"].Value;

            attributes[match.Groups[1].Value] = WebUtility.HtmlDecode(value);
        }

        return attributes;
    }

    /// <summary>
    /// Разрешает адрес иконки относительно страницы и проверяет, что это HTTP(S).
    /// </summary>
    private static bool TryResolveUri(Uri pageUri, string value, out Uri uri)
    {
        uri = null!;

        if (!Uri.TryCreate(pageUri, value.Trim(), out var resolved)
            || (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps)
            || resolved.Host.Length == 0)
        {
            return false;
        }

        uri = resolved;
        return true;
    }

    /// <summary>
    /// Разрешает адрес перенаправления относительно текущего адреса.
    /// </summary>
    private static bool TryResolveRedirect(Uri current, Uri? location, out Uri next)
    {
        next = null!;

        if (location is null)
        {
            return false;
        }

        var resolved = new Uri(current, location);
        if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        next = resolved;
        return true;
    }

    /// <summary>
    /// Читает ответ с ограничением размера, чтобы не вычитывать в память большой документ.
    /// </summary>
    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        while (buffer.Length < maxBytes)
        {
            var remaining = maxBytes - (int)buffer.Length;
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
        var match = _TitleRegex.Match(html);
        if (!match.Success)
        {
            return null;
        }

        var title = WebUtility.HtmlDecode(match.Groups["title"].Value);
        title = _WhitespaceRegex.Replace(title, " ").Trim();

        if (title.Length == 0)
        {
            return null;
        }

        return title.Length > DomainConstants.CardTitleMaxLength ? title[..DomainConstants.CardTitleMaxLength] : title;
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
