namespace Adashboard.Domain.Enums;

/// <summary>
/// Состояние сервиса карточки по результату проверки доступности.
/// </summary>
public enum HealthStatus
{
    /// <summary>Сервис отвечает и считается доступным.</summary>
    Online,

    /// <summary>Сервис недоступен или вернул недопустимый ответ.</summary>
    Offline,

    /// <summary>Состояние неизвестно: проверка отключена или не выполнена.</summary>
    Unknown
}
