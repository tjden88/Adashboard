using Adashboard.Domain.Contracts;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты общих настроек dashboard: чтение и обновление.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController(SiteSettingsService settingsService, ILogger<SettingsController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает текущие настройки dashboard.
    /// </summary>
    [HttpGet(Name = "GetSettings")]
    [ProducesResponseType<AppSettings>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AppSettings>> Get(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        return settings;
    }

    /// <summary>
    /// Обновляет настройки dashboard.
    /// </summary>
    [HttpPut(Name = "SaveSettings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put([FromBody] AppSettings? settings, CancellationToken cancellationToken)
    {
        if (settings is null)
        {
            logger.LogWarning("Обновление настроек отклонено: тело запроса пустое.");
            return BadRequest("Тело запроса с настройками обязательно.");
        }

        var result = await settingsService.UpdateAsync(settings, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}
