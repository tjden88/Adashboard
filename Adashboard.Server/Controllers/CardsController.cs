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
    [HttpPost(Name = "CreateCard")]
    [ProducesResponseType<DashboardCard>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardCard>> Create([FromBody] DashboardCard? card, CancellationToken cancellationToken)
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

        var result = await pageService.CreateCardAsync(card, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var created = result.Value;
        return Created($"/api/cards/{created.Id}", created);
    }

    /// <summary>
    /// Изменяет существующую карточку.
    /// </summary>
    [HttpPut(Name = "UpdateCard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] DashboardCard? card, CancellationToken cancellationToken)
    {
        if (card is null)
        {
            logger.LogWarning("Изменение карточки отклонено: тело запроса пустое.");
            return BadRequest(new { error = "Тело запроса с карточкой обязательно." });
        }

        var result = await pageService.UpdateCardAsync(card, cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    /// <summary>
    /// Удаляет карточку.
    /// </summary>
    [HttpDelete("{id:int}", Name = "DeleteCard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await pageService.DeleteCardAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    /// <summary>
    /// Сохраняет или обновляет кастомное изображение для карточки, и возвращает путь к нему.
    /// </summary>
    [HttpPost("{cardId}/image", Name = "UploadImage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(int cardId, [FromBody] byte[]? data, CancellationToken cancellationToken)
    {
        if (data is null || data.Length == 0)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} отклонена: файл не передан.", cardId);
            return BadRequest("Файл изображения не передан.");
        }

        var result = await imageIconService.SaveFromBytesAsync(cardId, data, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
