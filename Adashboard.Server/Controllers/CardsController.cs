using Adashboard.Domain.Contracts;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

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
    [HttpPost(Name = "CreateCard")]
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
    /// <param name="card">Тело запроса с новыми данными карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPut(Name = "UpdateCard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromBody] DashboardCard? card,
        CancellationToken cancellationToken)
    {
        if (card is null)
        {
            logger.LogWarning("Изменение карточки отклонено: тело запроса пустое.");
            return BadRequest(new { error = "Тело запроса с карточкой обязательно." });
        }

        var updated = await layoutService.UpdateCardAsync(card, cancellationToken);
        return updated ? NoContent() : NotFound(new { error = "Карточка не найдена." });
    }

    /// <summary>
    /// Удаляет карточку.
    /// </summary>
    /// <param name="id">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpDelete("{id:int}", Name = "DeleteCard")]
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
    /// <param name="data">Бинарные данные изображения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPost("{cardId}/image", Name = "UploadImage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        int cardId,
        [FromBody] byte[]? data,
        CancellationToken cancellationToken)
    {
        if (cardId <= 0)
        {
            logger.LogWarning("Загрузка изображения отклонена: некорректный идентификатор карточки {CardId}.", cardId);
            return BadRequest(new { error = "Некорректный идентификатор карточки." });
        }

        if (data is null || data.Length == 0)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: файл не передан.", cardId);
            return BadRequest(new { error = "Файл изображения не передан." });
        }

        if (data.Length > ImageIconService.MaxImageBytes)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: размер {Size} байт превышает лимит.", cardId, data.Length);
            return BadRequest(new { error = "Изображение превышает максимальный размер 2 МБ." });
        }

        try
        {

            var imagePath = await imageIconService.SaveFromBytesAsync(cardId, data, cancellationToken);
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
    /// <param name="url">Ссылка на изображение (параметр строки запроса <c>?url=...</c>).</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPost("{cardId}/image/from-url", Name = "UploadImageFromUrl")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFromUrl(
        int cardId,
        [FromQuery] string? url,
        CancellationToken cancellationToken)
    {
        if (cardId <= 0)
        {
            logger.LogWarning("Загрузка изображения отклонена: некорректный идентификатор карточки {CardId}.", cardId);
            return BadRequest(new { error = "Некорректный идентификатор карточки." });
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: ссылка не указана.", cardId);
            return BadRequest(new { error = "Укажите ссылку на изображение." });
        }

        try
        {
            var imagePath = await imageIconService.SaveFromUrlAsync(cardId, url, cancellationToken);
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
    [HttpDelete("{cardId}/image", Name = "DeleteImage")]
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
