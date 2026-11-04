using Adashboard.Domain.Contracts;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты общих настроек dashboard: чтение и обновление.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController(AppSettingsService settingsService, ILogger<SettingsController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает текущие настройки dashboard.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<AppSettings>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AppSettings>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        return settings;
    }

    /// <summary>
    /// Обновляет настройки dashboard.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveSettings([FromBody] AppSettings? settings, CancellationToken cancellationToken)
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
