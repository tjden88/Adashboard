using Adashboard.Domain.Contracts;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты управления категориями dashboard: создание, изменение и удаление.
/// </summary>
[ApiController]
[Route("api/categories")]
public class CategoriesController(
    LayoutService layoutService,
    ILogger<CategoriesController> logger) : ControllerBase
{
    /// <summary>
    /// Создаёт новую категорию в основной раскладке.
    /// </summary>
    /// <param name="category">Тело запроса с данными категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPost]
    [ProducesResponseType<DashboardCategory>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardCategory>> Create(
        [FromBody] DashboardCategory? category,
        CancellationToken cancellationToken)
    {
        if (category is null || string.IsNullOrWhiteSpace(category.Title))
        {
            logger.LogWarning("Создание категории отклонено: некорректное тело запроса.");
            return BadRequest(new { error = "Укажите название категории." });
        }

        var created = await layoutService.CreateCategoryAsync(category, cancellationToken);
        if (created is null)
        {
            return BadRequest();
        }

        return Created($"/api/categories/{created.Id}", created);
    }

    /// <summary>
    /// Изменяет существующую категорию.
    /// </summary>
    /// <param name="category">Тело запроса с новыми данными категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromBody] DashboardCategory? category,
        CancellationToken cancellationToken)
    {
        if (category is null || string.IsNullOrWhiteSpace(category.Title))
        {
            logger.LogWarning("Изменение категории {CategoryId} отклонено: некорректное тело запроса.", category?.Id);
            return BadRequest(new { error = "Укажите название категории." });
        }

        var updated = await layoutService.UpdateCategoryAsync(category, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>
    /// Удаляет категорию вместе с её карточками.
    /// </summary>
    /// <param name="id">Идентификатор категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await layoutService.DeleteCategoryAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
