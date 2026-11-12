using Adashboard.Domain.Contracts;
using Adashboard.Domain.ReadModels;
using Adashboard.Domain.Reference;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты страниц dashboard: список, полный снапшот с категориями и карточками, чтение,
/// создание, изменение и удаление.
/// </summary>
[ApiController]
[Route("api/pages")]
public class PageController(
    PageService pageService,
    ILogger<PageController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает список всех страниц dashboard.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DashboardPage>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DashboardPage>>> GetPages(CancellationToken cancellationToken)
    {
        var pages = await pageService.GetPagesAsync(cancellationToken);
        return Ok(pages);
    }

    /// <summary>
    /// Возвращает все страницы dashboard в порядке меню вместе с их категориями и карточками.
    /// </summary>
    [HttpGet("full")]
    [ProducesResponseType<IReadOnlyList<DashboardPageReadModel>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DashboardPageReadModel>>> GetFullPages(CancellationToken cancellationToken)
    {
        var pages = await pageService.GetFullPagesAsync(cancellationToken);
        return Ok(pages);
    }

    /// <summary>
    /// Возвращает страницу dashboard вместе с категориями и карточками.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<DashboardPageReadModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardPageReadModel>> GetPage(int id, CancellationToken cancellationToken)
    {
        var result = await pageService.GetPageAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Создаёт новую страницу dashboard.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DashboardPage>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardPage>> CreatePage([FromBody] DashboardPage? page, CancellationToken cancellationToken)
    {
        if (page is null || string.IsNullOrWhiteSpace(page.Title))
        {
            logger.LogWarning("Создание страницы отклонено: некорректное тело запроса.");
            return BadRequest("Укажите название страницы.");
        }

        if (!PagePathRules.IsValid(page.Path) || page.Path.Length == 0)
        {
            logger.LogWarning("Создание страницы отклонено: некорректный путь {Path}.", page.Path);
            return BadRequest("Укажите корректный путь страницы (латиница, цифры и дефис).");
        }

        var result = await pageService.CreatePageAsync(page, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var created = result.Value;
        return Created($"/api/pages/{created.Id}", created);
    }

    /// <summary>
    /// Изменяет заголовок, описание и путь страницы dashboard.
    /// </summary>
    [HttpPut("info")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SavePageInfo([FromBody] DashboardPage? info, CancellationToken cancellationToken)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.Title))
        {
            logger.LogWarning("Изменение страницы отклонено: некорректное тело запроса.");
            return BadRequest("Укажите название страницы.");
        }

        if (!PagePathRules.IsValid(info.Path))
        {
            logger.LogWarning("Изменение страницы отклонено: некорректный путь {Path}.", info.Path);
            return BadRequest("Укажите корректный путь страницы (латиница, цифры и дефис).");
        }

        var result = await pageService.UpdatePageInfoAsync(info, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    /// <summary>
    /// Применяет новый порядок страниц в боковом меню.
    /// </summary>
    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderPages([FromBody] IReadOnlyList<int>? pageIds, CancellationToken cancellationToken)
    {
        if (pageIds is null || pageIds.Count == 0)
        {
            logger.LogWarning("Перестановка страниц отклонена: пустой список страниц в теле запроса.");
            return BadRequest("Укажите порядок страниц.");
        }

        var result = await pageService.ReorderPagesAsync(pageIds, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    /// <summary>
    /// Удаляет страницу dashboard вместе с её категориями и карточками.
    /// Основную страницу удалить нельзя.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeletePage(int id, CancellationToken cancellationToken)
    {
        var result = await pageService.DeletePageAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}
