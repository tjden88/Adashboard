using Adashboard.Domain.Contracts;
using Adashboard.Domain.ReadModels;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты основной раскладки dashboard: чтение и изменение её информационной части.
/// </summary>
[ApiController]
[Route("api/layout")]
public class LayoutController(
    LayoutService layoutService,
    ILogger<LayoutController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает основную раскладку dashboard вместе с категориями и карточками.
    /// </summary>
    // Name задаёт operationId в OpenAPI-документе, а из него Refitter строит имя метода
    // в клиентском интерфейсе IDashboardApi (см. Adashboard.Client/dashboard-api.refitter).
    [HttpGet(Name = "GetLayout")]
    [ProducesResponseType<DashboardLayoutReadModel>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardLayoutReadModel>> Get(CancellationToken cancellationToken)
    {
        var layout = await layoutService.GetLayoutAsync(1, cancellationToken);
        return Ok(layout);
    }

    /// <summary>
    /// Изменяет заголовок и описание основной раскладки dashboard.
    /// </summary>
    /// <param name="info">Тело запроса с новыми заголовком и описанием.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPut("info", Name = "SaveLayoutInfo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutInfo(
        [FromBody] DashboardLayout? info,
        CancellationToken cancellationToken)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.Title))
        {
            logger.LogWarning("Изменение информационной части раскладки отклонено: некорректное тело запроса.");
            return BadRequest(new { error = "Укажите заголовок раскладки." });
        }

        var updated = await layoutService.UpdateLayoutAsync(info, cancellationToken);
        if (!updated)
        {
            return NotFound(new { error = "Раскладка не найдена." });
        }

        return NoContent();
    }
}
