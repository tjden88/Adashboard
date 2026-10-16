using System.Security.Claims;
using System.Text.Encodings.Web;
using Adashboard.Domain.Reference;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Adashboard.Server.Auth;

/// <summary>
/// Аутентификация по ключу доступа: ключ приходит в заголовке X-Api-Key.
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

        var provided = Request.Headers[DomainConstants.ApiKeyHeaderName].ToString();
        if (!apiKeySettings.Matches(provided))
        {
            return Task.FromResult(AuthenticateResult.Fail("Ключ доступа не указан или неверен."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, SchemeName)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Отказ фиксируем в логе, но сам ключ не пишем.
        Logger.LogWarning("Запрос к API отклонён: ключ доступа отсутствует или неверен. Путь: {Path}.", Request.Path);

        // Отказ отдаём телом-текстом, как и остальные ответы API.
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "text/plain; charset=utf-8";
        await Response.WriteAsync("Требуется ключ доступа.");
    }
}
