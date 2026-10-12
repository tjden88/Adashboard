using Adashboard.Api.Services;
using Adashboard.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Api.Controllers;

/// <summary>
/// Эндпоинты управления карточками dashboard и их изображениями-иконками.
/// </summary>
[ApiController]
[Route("api/cards")]
public class CardsController(
    LayoutService layoutService,
    ImageIconService imageIconService,
    ILogger<CardsController> logger) : ControllerBase
{
    /// <summary>
    /// Создаёт новую карточку в указанной категории.
    /// </summary>
    /// <param name="card">Тело запроса с данными карточки (включая <c>DashboardCategoryId</c>).</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPost]
    [ProducesResponseType<DashboardCard>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardCard>> Create(
        [FromBody] DashboardCard? card,
        CancellationToken cancellationToken)
    {
        if (card is null)
        {
            logger.LogWarning("Создание карточки отклонено: тело запроса пустое.");
            return BadRequest(new { error = "Тело запроса с карточкой обязательно." });
        }

        if (card.DashboardCategoryId <= 0)
        {
            logger.LogWarning("Создание карточки отклонено: не указана категория.");
            return BadRequest(new { error = "Укажите категорию карточки." });
        }

        var created = await layoutService.CreateCardAsync(card, cancellationToken);
        if (created is null)
        {
            return NotFound(new { error = "Категория не найдена." });
        }

        return Created($"/api/cards/{created.Id}", created);
    }

    /// <summary>
    /// Изменяет существующую карточку.
    /// </summary>
    /// <param name="id">Идентификатор карточки.</param>
    /// <param name="card">Тело запроса с новыми данными карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] DashboardCard? card,
        CancellationToken cancellationToken)
    {
        if (card is null)
        {
            logger.LogWarning("Изменение карточки {CardId} отклонено: тело запроса пустое.", id);
            return BadRequest(new { error = "Тело запроса с карточкой обязательно." });
        }

        var updated = await layoutService.UpdateCardAsync(id, card, cancellationToken);
        return updated ? NoContent() : NotFound(new { error = "Карточка не найдена." });
    }

    /// <summary>
    /// Удаляет карточку.
    /// </summary>
    /// <param name="id">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await layoutService.DeleteCardAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound(new { error = "Карточка не найдена." });
    }

    /// <summary>
    /// Сохраняет изображение, загруженное с устройства, и возвращает путь к нему.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки, для которой сохраняется изображение.</param>
    /// <param name="file">Загруженный файл изображения (multipart/form-data, поле <c>file</c>).</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPost("{cardId}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        int cardId,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (cardId <= 0)
        {
            logger.LogWarning("Загрузка изображения отклонена: некорректный идентификатор карточки {CardId}.", cardId);
            return BadRequest(new { error = "Некорректный идентификатор карточки." });
        }

        if (file is null || file.Length == 0)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: файл не передан.", cardId);
            return BadRequest(new { error = "Файл изображения не передан." });
        }

        if (file.Length > ImageIconService.MaxImageBytes)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: размер {Size} байт превышает лимит.", cardId, file.Length);
            return BadRequest(new { error = "Изображение превышает максимальный размер 2 МБ." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);

            var imagePath = await imageIconService.SaveFromBytesAsync(cardId, buffer.ToArray(), cancellationToken);
            return Ok(new { imagePath });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: {Reason}", cardId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Скачивает изображение по ссылке и сохраняет его для карточки.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки, для которой сохраняется изображение.</param>
    /// <param name="request">Тело запроса со ссылкой на изображение.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPost("{cardId}/image/from-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFromUrl(
        int cardId,
        [FromBody] CardImageUrlRequest? request,
        CancellationToken cancellationToken)
    {
        if (cardId <= 0)
        {
            logger.LogWarning("Загрузка изображения отклонена: некорректный идентификатор карточки {CardId}.", cardId);
            return BadRequest(new { error = "Некорректный идентификатор карточки." });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Url))
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: ссылка не указана.", cardId);
            return BadRequest(new { error = "Укажите ссылку на изображение." });
        }

        try
        {
            var imagePath = await imageIconService.SaveFromUrlAsync(cardId, request.Url, cancellationToken);
            return Ok(new { imagePath });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} по ссылке отклонена: {Reason}", cardId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Удаляет все сохранённые изображения карточки.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    [HttpDelete("{cardId}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult DeleteImage(int cardId)
    {
        if (cardId <= 0)
        {
            logger.LogWarning("Удаление изображения отклонено: некорректный идентификатор карточки {CardId}.", cardId);
            return BadRequest(new { error = "Некорректный идентификатор карточки." });
        }

        imageIconService.Delete(cardId);
        return NoContent();
    }
}

/// <summary>
/// Тело запроса загрузки изображения карточки по ссылке.
/// </summary>
/// <param name="Url">Абсолютная HTTP(S)-ссылка на изображение.</param>
public sealed record CardImageUrlRequest(string? Url);
