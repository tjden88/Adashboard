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

        if (response is {IsSuccessStatusCode: false, Error: ApiException apiException})
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
