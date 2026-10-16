using System.Net;
using Adashboard.Client.Services;
using Adashboard.Domain.Reference;

namespace Adashboard.Client.Helpers;

/// <summary>
/// Добавляет в запросы к API ключ доступа и сообщает о его отклонении сервером.
/// </summary>
public sealed class ApiKeyHandler(ApiKeyStore store) : DelegatingHandler
{
    /// <summary>
    /// Подставляет текущий ключ доступа в заголовок запроса и следит за ответом 401.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = store.Key;
        if (!string.IsNullOrEmpty(key))
        {
            request.Headers.TryAddWithoutValidation(DomainConstants.ApiKeyHeaderName, key);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Ключ отозван или изменён на сервере: приложение вернётся к экрану входа.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await store.NotifyUnauthorizedAsync();
        }

        return response;
    }
}
