using Adashboard.Domain.Contracts;
using Adashboard.Domain.ReadModels;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты управления категориями dashboard: создание, изменение и удаление.
/// </summary>
[ApiController]
[Route("api/categories")]
public class CategoriesController(
    PageService pageService,
    ILogger<CategoriesController> logger) : ControllerBase
{
    /// <summary>
    /// Создаёт новую категорию на указанной странице.
    /// </summary>
    [HttpPost(Name = "CreateCategory")]
    [ProducesResponseType<DashboardCategory>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardCategory>> Create([FromBody] DashboardCategory? category, CancellationToken cancellationToken)
    {
        if (category is null || string.IsNullOrWhiteSpace(category.Title))
        {
            logger.LogWarning("Создание категории отклонено: некорректное тело запроса.");
            return BadRequest("Укажите название категории.");
        }

        var result = await pageService.CreateCategoryAsync(category, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var created = result.Value;
        return Created($"/api/categories/{created.Id}", created);
    }

    /// <summary>
    /// Изменяет существующую категорию.
    /// </summary>
    [HttpPut(Name = "UpdateCategory")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] DashboardCategory? category, CancellationToken cancellationToken)
    {
        if (category is null || string.IsNullOrWhiteSpace(category.Title))
        {
            logger.LogWarning("Изменение категории {CategoryId} отклонено: некорректное тело запроса.", category?.Id);
            return BadRequest("Укажите название категории.");
        }

        var result = await pageService.UpdateCategoryAsync(category, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    /// <summary>
    /// Создаёт копию категории со всеми карточками сразу после исходной.
    /// </summary>
    [HttpPost("{id:int}/duplicate", Name = "DuplicateCategory")]
    [ProducesResponseType<DashboardCategoryReadModel>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardCategoryReadModel>> Duplicate(int id, CancellationToken cancellationToken)
    {
        var result = await pageService.DuplicateCategoryAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            // Отказ здесь только один: категории нет.
            return NotFound(result.Error);
        }

        var created = result.Value;
        return Created($"/api/categories/{created.Category.Id}", created);
    }

    /// <summary>
    /// Удаляет категорию вместе с её карточками.
    /// </summary>
    /// <param name="id">Идентификатор категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpDelete("{id:int}", Name = "DeleteCategory")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await pageService.DeleteCategoryAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}
