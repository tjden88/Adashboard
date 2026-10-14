using Adashboard.Domain.ReadModels;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты серверной проверки карточек: доступность сервиса и заголовок страницы.
/// </summary>
[ApiController]
[Route("api/probe")]
public class ProbeController(
    ProbeService probeService,
    ILogger<ProbeController> logger) : ControllerBase
{
    /// <summary>
    /// Проверяет доступность сервиса карточки по её идентификатору.
    /// </summary>
    [HttpGet("health/{cardId:int}", Name = "CheckHealth")]
    [ProducesResponseType<HealthCheckResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HealthCheckResult>> GetHealth(int cardId, CancellationToken cancellationToken)
    {
        if (cardId <= 0)
        {
            logger.LogWarning("Проверка доступности отклонена: некорректный идентификатор карточки {CardId}.", cardId);
            return BadRequest(new { error = "Некорректный идентификатор карточки." });
        }

        var result = await probeService.CheckCardAsync(cardId, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var check = result.Value;
        return check;
    }

    /// <summary>
    /// Пытается получить заголовок страницы по указанному адресу.
    /// </summary>
    [HttpGet("title", Name = "GetTitle")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTitle([FromQuery] string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("Получение заголовка отклонено: ссылка не указана.");
            return BadRequest("Укажите ссылку на страницу.");
        }

        var result = await probeService.GetTitleAsync(url, cancellationToken);

        // Отказ — это некорректная ссылка или недоступная страница, поэтому отвечаем 400.
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
