using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adashboard.Server.Controllers;

/// <summary>
/// Служебные эндпоинты API: проверка работоспособности и проверка ключа доступа.
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Возвращает статус работоспособности API без проверки ключа доступа.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "ok" });
    }

    // Защиту обеспечивает общее требование авторизации API (см. Program.cs): при заданном
    // ключе без него эндпоинт недоступен, а без ключа защита выключена целиком.
    /// <summary>
    /// Подтверждает действующий ключ доступа: клиент использует метод для проверки входа.
    /// </summary>
    [HttpGet("access")]
    public IActionResult CheckAccess()
    {
        return Ok();
    }
}
