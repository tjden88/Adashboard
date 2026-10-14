using Adashboard.Domain.Contracts;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты общих настроек dashboard: чтение и обновление.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController(
    SiteSettingsService settingsService,
    ILogger<SettingsController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает текущие настройки dashboard.
    /// </summary>
    [HttpGet(Name = "GetSettings")]
    [ProducesResponseType<SiteSettings>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SiteSettings>> Get(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Обновляет настройки dashboard.
    /// </summary>
    /// <param name="settings">Тело запроса с новыми настройками.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPut(Name = "SaveSettings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put(
        [FromBody] SiteSettings? settings,
        CancellationToken cancellationToken)
    {
        if (settings is null)
        {
            logger.LogWarning("Обновление настроек отклонено: тело запроса пустое.");
            return BadRequest(new { error = "Тело запроса с настройками обязательно." });
        }

        if (settings.HealthCheckIntervalSeconds < 0)
        {
            logger.LogWarning("Обновление настроек отклонено: некорректный интервал проверки статуса.");
            return BadRequest(new { error = "Интервал проверки статуса должен быть больше либо равен нулю." });
        }

        await settingsService.UpdateAsync(settings, cancellationToken);
        return NoContent();
    }
}
