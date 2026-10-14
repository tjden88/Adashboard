using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Служебный эндпоинт для проверки, что API запущено и отвечает.
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Возвращает статус работоспособности API.
    /// </summary>
    [HttpGet(Name = "GetHealth")]
    public IActionResult Get()
    {
        return Ok(new { status = "ok" });
    }
}
