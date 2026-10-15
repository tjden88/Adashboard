using System.Net;
using System.Text;

namespace Adashboard.Client.Services;

/// <summary>
/// Превращает сетевой сбой HTTP-запроса в обычный ответ с кодом статуса.
/// </summary>
public sealed class NetworkErrorHandler : DelegatingHandler
{
    /// <summary>
    /// Выполняет запрос, а при сбое сети возвращает синтетический ответ с кодом и текстом ошибки.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Операцию отменил вызывающий код: это не ошибка, отмену пробрасываем дальше.
            throw;
        }
        catch (TaskCanceledException)
        {
            // Токен не отменён, значит истёк таймаут запроса.
            return CreateFailureResponse(request, HttpStatusCode.GatewayTimeout, "Сервер не ответил вовремя.");
        }
        catch (HttpRequestException)
        {
            return CreateFailureResponse(request, HttpStatusCode.ServiceUnavailable, "Нет связи с сервером.");
        }
    }

    /// <summary>
    /// Создаёт синтетический ответ сбоя с текстом ошибки в том же формате, что отдаёт сервер.
    /// </summary>
    private static HttpResponseMessage CreateFailureResponse(HttpRequestMessage request, HttpStatusCode statusCode, string message)
        => new(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(message, Encoding.UTF8, "text/plain")
        };
}
