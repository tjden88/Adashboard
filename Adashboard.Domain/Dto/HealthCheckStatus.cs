using static Adashboard.Domain.Dto.HealthCheckStatus;

namespace Adashboard.Domain.Dto;

/// <summary>
/// Результат проверки доступности сервиса карточки: состояние и понятное пояснение.
/// </summary>
/// <param name="Status">Состояние сервиса.</param>
/// <param name="Message">Короткое пояснение исхода — без внутренних адресов и деталей ошибок.</param>
public record HealthCheckStatus(HealthStatus Status, string Message)
{
    /// <summary>
    /// Состояние сервиса.
    /// </summary>
    public enum HealthStatus
    {
        Online,
        Offline,
        Unknown
    }
}