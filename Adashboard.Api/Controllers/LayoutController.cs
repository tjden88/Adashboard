using Adashboard.Api.Data;
using Adashboard.Api.Models.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Api.Controllers;

/// <summary>
/// Эндпоинты чтения и полного сохранения основной раскладки dashboard.
/// </summary>
[ApiController]
[Route("api/layout")]
public class LayoutController(
    IDashboardLayoutService layoutService,
    ILogger<LayoutController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает основную раскладку dashboard вместе с категориями и карточками.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<DashboardLayout>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardLayout>> Get(CancellationToken cancellationToken)
    {
        var layout = await layoutService.GetMainLayoutAsync(cancellationToken);
        return Ok(layout);
    }

    /// <summary>
    /// Полностью заменяет основную раскладку dashboard.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put([FromBody] DashboardLayout? layout, CancellationToken cancellationToken)
    {
        if (layout is null)
        {
            logger.LogWarning("Сохранение раскладки отклонено: тело запроса пустое.");
            return BadRequest(new { error = "Тело запроса с раскладкой обязательно." });
        }

        await layoutService.SaveMainLayoutAsync(layout, cancellationToken);
        return NoContent();
    }
}
