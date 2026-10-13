using Adashboard.Domain.Enums;

namespace Adashboard.Domain.ReadModels;

/// <summary>
/// Результат проверки доступности сервиса карточки: состояние и понятное пояснение.
/// </summary>
/// <param name="Status">Состояние сервиса.</param>
/// <param name="Message">Короткое пояснение исхода — без внутренних адресов и деталей ошибок.</param>
public record HealthCheckStatus(HealthStatus Status, string Message);
