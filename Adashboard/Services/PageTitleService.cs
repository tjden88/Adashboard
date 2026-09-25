using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Adashboard.Services;

/// <summary>
/// Получает заголовок HTML-страницы по адресу карточки.
/// </summary>
public interface IPageTitleService
{
    /// <summary>
    /// Пытается получить содержимое тега &lt;title&gt; по указанному адресу.
    /// </summary>
    /// <param name="url">Абсолютная HTTP(S)-ссылка на страницу.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Заголовок страницы или <c>null</c>, если он не найден.</returns>
    Task<string?> TryGetTitleAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Реализация <see cref="IPageTitleService"/> через HTTP-запрос и разбор HTML.
/// </summary>
/// <remarks>
/// Как и <see cref="HealthCheckService"/>, сервис обращается к домашним сервисам карточек,
/// поэтому ограничение SSRF намеренно не применяется.
/// </remarks>
public sealed class PageTitleService(
    IHttpClientFactory httpClientFactory,
    ILogger<PageTitleService> logger) : IPageTitleService
{
    private const string HttpClientName = "PageTitle";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
    private const int MaxHtmlBytes = 256 * 1024;
    private const int MaxTitleLength = 120;

    // Захватываем содержимое тега title без учёта регистра и переносов строк.
    private static readonly Regex TitleRegex = new(
        @"<title[^>]*>(?<title>.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<PageTitleService> _logger = logger;

    /// <inheritdoc />
    public async Task<string?> TryGetTitleAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!TryBuildAbsoluteUri(url, out var uri))
        {
            _logger.LogWarning("Получение заголовка отклонено: адрес не является абсолютным HTTP(S)-адресом.");
            throw new InvalidOperationException("Укажите корректную HTTP(S)-ссылку.");
        }

        _logger.LogInformation("Начато получение заголовка страницы на хосте {Host}.", uri.Host);

        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(RequestTimeout);

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Получение заголовка завершилось неудачно. Код ответа: {StatusCode}.", (int)response.StatusCode);
                throw new InvalidOperationException($"Страница вернула ошибку (код {(int)response.StatusCode}).");
            }

            var content = await ReadLimitedAsync(response.Content, cancellationToken);
            var html = DecodeHtml(content, response.Content.Headers.ContentType?.CharSet);
            var title = ExtractTitle(html);

            if (title is null)
            {
                _logger.LogWarning("На странице хоста {Host} не найден тег title.", uri.Host);
                return null;
            }

            _logger.LogInformation("Заголовок страницы на хосте {Host} успешно получен.", uri.Host);
            return title;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Получение заголовка не завершилось за отведённое время.");
            throw new InvalidOperationException("Страница не ответила за отведённое время.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Получение заголовка завершилось сетевой ошибкой: {Message}", ex.Message);
            throw new InvalidOperationException("Не удалось подключиться к сервису.");
        }
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
    /// Собирает абсолютный HTTP(S)-адрес из строки ссылки.
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
