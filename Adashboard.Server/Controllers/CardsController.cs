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
    PageService pageService,
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

        var created = await pageService.CreateCardAsync(card, cancellationToken);
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

        var updated = await pageService.UpdateCardAsync(card, cancellationToken);
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
        var deleted = await pageService.DeleteCardAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound(new { error = "Карточка не найдена." });
    }

    /// <summary>
    /// Сохраняет или обновляет кастомное изображение для карточки, и возвращает путь к нему.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки, для которой сохраняется изображение.</param>
    /// <param name="data">Бинарные данные изображения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPost("{cardId}/image", Name = "UploadImage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(int cardId, [FromBody] byte[]? data, CancellationToken cancellationToken)
    {
        if (cardId <= 0)
        {
            logger.LogWarning("Загрузка изображения отклонена: некорректный идентификатор карточки {CardId}.", cardId);
            return BadRequest( "Некорректный идентификатор карточки.");
        }

        if (data is null || data.Length == 0)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: файл не передан.", cardId);
            return BadRequest("Файл изображения не передан.");
        }

        if (data.Length > ImageIconService.MaxImageBytes)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: размер {Size} байт превышает лимит.", cardId, data.Length);
            return BadRequest("Изображение превышает максимальный размер 2 МБ.");
        }

        try
        {
            var imagePath = await imageIconService.SaveFromBytesAsync(cardId, data, cancellationToken);
            return Ok(imagePath);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: {Reason}", cardId, ex.Message);
            return BadRequest(ex.Message);
        }
    }

}
