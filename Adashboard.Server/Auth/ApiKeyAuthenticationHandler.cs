using System.Security.Claims;
using System.Text.Encodings.Web;
using Adashboard.Domain.Reference;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Adashboard.Server.Auth;

/// <summary>
/// Аутентификация по ключу доступа: ключ приходит в заголовке X-Api-Key.
/// Подходит основной ключ или ключ расширения браузера — доступ по ним одинаковый.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ApiKeySettings apiKeySettings) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    /// <summary>
    /// Имя схемы аутентификации по ключу доступа.
    /// </summary>
    public const string SchemeName = "ApiKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Ключ не настроен: защита выключена, запрос обрабатывается как анонимный.
        if (!apiKeySettings.IsEnabled)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Logger.LogDebug("Проверяю ключ доступа к API. Путь: {Path}.", Request.Path);
        var provided = Request.Headers[DomainConstants.ApiKeyHeaderName].ToString();
        if (!apiKeySettings.Matches(provided))
        {
            return Task.FromResult(AuthenticateResult.Fail("Ключ доступа не указан или не совпадает с основным ключом или ключом расширения."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, SchemeName)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Отказ фиксируем в логе, но сами ключи не пишем.
        Logger.LogWarning("Запрос к API отклонён: ключ доступа отсутствует или не совпадает с основным ключом или ключом расширения. Путь: {Path}.", Request.Path);

        // Отказ отдаём телом-текстом, как и остальные ответы API.
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "text/plain; charset=utf-8";
        await Response.WriteAsync("Требуется ключ доступа.");
    }
}
