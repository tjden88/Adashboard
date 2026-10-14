using Refit;

namespace Adashboard.Client.Services;

/// <summary>
/// Помощники чтения ответов API, который отдаёт отказы телом-текстом (см. AGENTS.md).
/// </summary>
public static class ApiResponseExtensions
{
    /// <summary>
    /// Возвращает понятный текст ошибки из ответа API.
    /// </summary>
    /// <remarks>
    /// Сообщение отказа лежит в теле ответа как обычный текст, поэтому сначала берём его,
    /// а при пустом теле подставляем общий текст с кодом ответа.
    /// </remarks>
    public static string ErrorText(this IApiResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.IsSuccessStatusCode && response.Error is ApiException apiException)
        {
            var message = apiException.Content?.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                return message;
            }
        }

        var statusCode = response.StatusCode is { } status ? (int)status : 0;
        return $"Не удалось выполнить запрос (код {statusCode}).";
    }
}
