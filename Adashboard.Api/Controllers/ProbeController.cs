using Adashboard.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Api.Controllers;

/// <summary>
/// Эндпоинты серверной проверки карточек: доступность сервиса и заголовок страницы.
/// </summary>
/// <remarks>
/// WASM-клиент не может обращаться к целевым сервисам напрямую из-за политики CORS, поэтому
/// запросы выполняет backend (см. <see cref="ProbeService"/>).
/// </remarks>
[ApiController]
[Route("api/probe")]
public class ProbeController(
    ProbeService probeService,
    ILogger<ProbeController> logger) : ControllerBase
{
    /// <summary>
    /// Проверяет доступность сервиса карточки по её идентификатору.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpGet("health/{cardId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHealth(int cardId, CancellationToken cancellationToken)
    {
        if (cardId <= 0)
        {
            logger.LogWarning("Проверка доступности отклонена: некорректный идентификатор карточки {CardId}.", cardId);
            return BadRequest(new { error = "Некорректный идентификатор карточки." });
        }

        var result = await probeService.CheckCardAsync(cardId, cancellationToken);
        if (result is null)
        {
            logger.LogWarning("Проверка доступности отклонена: карточка {CardId} не найдена.", cardId);
            return NotFound(new { error = "Карточка не найдена." });
        }

        return Ok(new { status = result.Status, message = result.Message });
    }

    /// <summary>
    /// Пытается получить заголовок страницы по указанному адресу.
    /// </summary>
    /// <param name="url">Абсолютная HTTP(S)-ссылка на страницу.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpGet("title")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTitle([FromQuery] string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("Получение заголовка отклонено: ссылка не указана.");
            return BadRequest(new { error = "Укажите ссылку на страницу." });
        }

        try
        {
            var title = await probeService.GetTitleAsync(url, cancellationToken);
            return Ok(new { title });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Получение заголовка отклонено: {Reason}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }
}
