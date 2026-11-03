using Refit;

namespace Adashboard.Client.Helpers;

/// <summary>
/// Помощники чтения ответов API, который отдаёт отказы телом-текстом.
/// </summary>
public static class ApiResponseExtensions
{
    /// <summary>
    /// Возвращает понятный текст ошибки из ответа API.
    /// </summary>
    public static string ErrorText(this IApiResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var statusCode = response.StatusCode is { } status ? (int)status : 0;

        if (response is {IsSuccessStatusCode: false, Error: ApiException apiException})
        {
            var message = apiException.Content?.Trim();
            var mediaType = apiException.ContentHeaders?.ContentType?.MediaType;

            // Ошибки API приходят простым текстом (text/plain). HTML и прочие тела —
            // например, страницу 502 от reverse proxy — в интерфейс не показываем:
            // пользователю нужен понятный текст, а не разметка чужого сервера.
            if (!string.IsNullOrEmpty(message) && !LooksLikeMarkup(message) && IsPlainText(mediaType))
            {
                return message;
            }
        }

        // Прокси отвечает 502/503/504, когда приложение недоступно: подсказываем,
        // что делать, вместо кода незнакомой ошибки.
        return statusCode switch
        {
            502 or 503 or 504 => $"Сервер временно недоступен (код {statusCode}). Проверьте, что сервер запущен, и повторите попытку.",
            _ => $"Не удалось выполнить запрос (код {statusCode})."
        };
    }

    // Тело ошибки может оказаться HTML-страницей прокси даже с текстовым типом содержимого.
    private static bool LooksLikeMarkup(string content) => content.TrimStart().StartsWith('<');

    // Показываем только простой текст: остальные типы (HTML прокси, JSON) в UI не годятся.
    private static bool IsPlainText(string? mediaType) =>
        mediaType is null || string.Equals(mediaType, "text/plain", StringComparison.OrdinalIgnoreCase);
}
