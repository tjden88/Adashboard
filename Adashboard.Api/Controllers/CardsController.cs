using Adashboard.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Api.Controllers;

/// <summary>
/// Эндпоинты работы с изображением-иконкой карточки: загрузка из файла, загрузка по ссылке и удаление.
/// </summary>
[ApiController]
[Route("api/cards")]
public class CardsController(
    ImageIconService imageIconService,
    ILogger<CardsController> logger) : ControllerBase
{
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
    public IActionResult Delete(int cardId)
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
