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

    /// <summary>
    /// Возвращает строковое представление состояния сервиса на русском языке.
    /// </summary>
    public string StatusText => Status switch
    {
        HealthStatus.Online => "Онлайн",
        HealthStatus.Offline => "Оффлайн",
        HealthStatus.Unknown => "Неизвестно",
        _ => throw new ArgumentOutOfRangeException()
    };
}