using Adashboard.Domain.Contracts;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты управления карточками dashboard.
/// </summary>
[ApiController]
[Route("api/cards")]
public class CardsController(PageService pageService, ILogger<CardsController> logger) : ControllerBase
{
    /// <summary>
    /// Создаёт новую карточку в указанной категории, при необходимости с изображением-иконкой.
    /// </summary>
    [HttpPost(Name = "CreateCard")]
    [ProducesResponseType<DashboardCard>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardCard>> Create([FromBody] SaveCardRequest? request, CancellationToken cancellationToken)
    {
        if (request?.Card is null)
        {
            logger.LogWarning("Создание карточки отклонено: тело запроса пустое.");
            return BadRequest("Тело запроса с карточкой обязательно.");
        }

        if (request.Card.DashboardCategoryId <= 0)
        {
            logger.LogWarning("Создание карточки отклонено: не указана категория.");
            return BadRequest("Укажите категорию карточки.");
        }

        var result = await pageService.CreateCardAsync(request.Card, request.ImageContent, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var created = result.Value;
        return Created($"/api/cards/{created.Id}", created);
    }

    /// <summary>
    /// Изменяет существующую карточку, при необходимости заменяя или удаляя изображение-иконку.
    /// </summary>
    [HttpPut(Name = "UpdateCard")]
    [ProducesResponseType<DashboardCard>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardCard>> Update([FromBody] SaveCardRequest? request, CancellationToken cancellationToken)
    {
        if (request?.Card is null)
        {
            logger.LogWarning("Изменение карточки отклонено: тело запроса пустое.");
            return BadRequest("Тело запроса с карточкой обязательно.");
        }

        var result = await pageService.UpdateCardAsync(request.Card, request.ImageContent, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return result.Value;
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
}
