using Adashboard.Domain.Enums;

namespace Adashboard.Domain.ReadModels;

/// <summary>
/// Результат проверки доступности сервиса карточки: состояние и понятное пояснение.
/// </summary>
/// <remarks>
/// Одиночный неизменяемый результат операции чтения, а не агрегат: поэтому без суффикса
/// <c>*ReadModel</c>, который зарезервирован под read-модели с вложенностью.
/// </remarks>
/// <param name="Status">Состояние сервиса.</param>
/// <param name="Message">Короткое пояснение исхода — без внутренних адресов и деталей ошибок.</param>
public record HealthCheckResult(HealthStatus Status, string Message);
