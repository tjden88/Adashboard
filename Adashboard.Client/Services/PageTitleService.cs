using Refit;

namespace Adashboard.Client.Services;

/// <summary>
/// Сервис получения заголовка HTML-страницы по адресу карточки.
/// </summary>
/// <remarks>
/// Разбор HTML выполняет Adashboard.Server: браузер не может читать ответы целевых сервисов напрямую
/// из-за политики CORS. Клиент только передаёт адрес и получает готовый заголовок.
/// </remarks>
public sealed class PageTitleService(
    IDashboardApi dashboardApi,
    ILogger<PageTitleService> logger)
{
    /// <summary>
    /// Пытается получить содержимое тега &lt;title&gt; по указанному адресу.
    /// </summary>
    /// <param name="url">Абсолютная HTTP(S)-ссылка на страницу.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Заголовок страницы или <c>null</c>, если он не найден.</returns>
    public async Task<string?> TryGetTitleAsync(string url, CancellationToken cancellationToken = default)
    {
        // Ранняя проверка адреса: она даёт точное сообщение без лишнего запроса к API.
        if (!TryBuildAbsoluteUri(url, out var uri))
        {
            logger.LogWarning("Получение заголовка отклонено: адрес не является абсолютным HTTP(S)-адресом.");
            throw new InvalidOperationException("Укажите корректную HTTP(S)-ссылку.");
        }

        logger.LogInformation("Запрошено получение заголовка страницы на хосте {Host} через API.", uri.Host);

        try
        {
            var response = await dashboardApi.GetTitleAsync(url, cancellationToken);
            var title = response?.Title;

            if (string.IsNullOrWhiteSpace(title))
            {
                logger.LogWarning("Заголовок страницы на хосте {Host} не найден.", uri.Host);
                return null;
            }

            logger.LogInformation("Заголовок страницы на хосте {Host} успешно получен.", uri.Host);
            return title;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiException ex)
        {
            logger.LogWarning(
                "Получение заголовка завершилось ошибкой API. Код: {StatusCode}.",
                ex.StatusCode);
            throw new InvalidOperationException("Не удалось получить заголовок страницы.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Получение заголовка завершилось сетевой ошибкой: {Message}", ex.Message);
            throw new InvalidOperationException("Не удалось подключиться к сервису.");
        }
    }

    /// <summary>
    /// Проверяет, что адрес является абсолютным HTTP(S)-адресом с непустым хостом.
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
