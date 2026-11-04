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
    /// Возвращает все категории dashboard: клиент показывает их с группировкой по страницам.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DashboardCategory>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DashboardCategory>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await pageService.GetCategoriesAsync(cancellationToken);
        return Ok(categories);
    }

    /// <summary>
    /// Создаёт новую категорию на указанной странице.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DashboardCategory>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardCategory>> CreateCategory([FromBody] DashboardCategory? category, CancellationToken cancellationToken)
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
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCategory([FromBody] DashboardCategory? category, CancellationToken cancellationToken)
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
    [HttpPost("{id:int}/duplicate")]
    [ProducesResponseType<DashboardCategoryReadModel>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardCategoryReadModel>> DuplicateCategory(int id, CancellationToken cancellationToken)
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
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken)
    {
        var result = await pageService.DeleteCategoryAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}
