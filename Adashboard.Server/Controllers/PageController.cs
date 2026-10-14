using Adashboard.Domain.Contracts;
using Adashboard.Domain.ReadModels;
using Adashboard.Domain.Reference;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Эндпоинты страниц dashboard: список, чтение, создание, изменение и удаление.
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
    // Name задаёт operationId в OpenAPI-документе, а из него Refitter строит имя метода
    // в клиентском интерфейсе IDashboardApi (см. Adashboard.Client/dashboard-api.refitter).
    [HttpGet(Name = "GetPages")]
    [ProducesResponseType<IReadOnlyList<DashboardPage>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DashboardPage>>> GetPages(CancellationToken cancellationToken)
    {
        var pages = await pageService.GetPagesAsync(cancellationToken);
        return Ok(pages);
    }

    /// <summary>
    /// Возвращает страницу dashboard вместе с категориями и карточками.
    /// </summary>
    /// <param name="id">Идентификатор страницы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpGet("{id:int}", Name = "GetPage")]
    [ProducesResponseType<DashboardPageReadModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardPageReadModel>> GetPage(int id, CancellationToken cancellationToken)
    {
        var result = await pageService.GetPageAsync(id, cancellationToken);
        return result.ToActionResult(page => Ok(page));
    }

    /// <summary>
    /// Создаёт новую страницу dashboard.
    /// </summary>
    /// <param name="page">Тело запроса с данными страницы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPost(Name = "CreatePage")]
    [ProducesResponseType<DashboardPage>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardPage>> Create(
        [FromBody] DashboardPage? page,
        CancellationToken cancellationToken)
    {
        if (page is null || string.IsNullOrWhiteSpace(page.Title))
        {
            logger.LogWarning("Создание страницы отклонено: некорректное тело запроса.");
            return BadRequest(new { error = "Укажите название страницы." });
        }

        if (!PagePathRules.IsValid(page.Path) || page.Path.Length == 0)
        {
            logger.LogWarning("Создание страницы отклонено: некорректный путь {Path}.", page.Path);
            return BadRequest(new { error = "Укажите корректный путь страницы (латиница, цифры и дефис)." });
        }

        var result = await pageService.CreatePageAsync(page, cancellationToken);
        return result.ToActionResult(created => Created($"/api/pages/{created.Id}", created));
    }

    /// <summary>
    /// Изменяет заголовок, описание и путь страницы dashboard.
    /// </summary>
    /// <param name="info">Тело запроса с новыми данными страницы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpPut("info", Name = "SavePageInfo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutInfo(
        [FromBody] DashboardPage? info,
        CancellationToken cancellationToken)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.Title))
        {
            logger.LogWarning("Изменение страницы отклонено: некорректное тело запроса.");
            return BadRequest(new { error = "Укажите название страницы." });
        }

        if (!PagePathRules.IsValid(info.Path))
        {
            logger.LogWarning("Изменение страницы отклонено: некорректный путь {Path}.", info.Path);
            return BadRequest(new { error = "Укажите корректный путь страницы (латиница, цифры и дефис)." });
        }

        var result = await pageService.UpdatePageInfoAsync(info, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Удаляет страницу dashboard вместе с её категориями и карточками.
    /// Основную страницу удалить нельзя.
    /// </summary>
    /// <param name="id">Идентификатор страницы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [HttpDelete("{id:int}", Name = "DeletePage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await pageService.DeletePageAsync(id, cancellationToken);
        return result.ToActionResult();
    }
}
