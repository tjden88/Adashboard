using Adashboard.Domain.Dto;
using Adashboard.Domain.Models;

namespace Adashboard.Domain.Extensions;

/// <summary>
/// Вспомогательные методы для <see cref="UrlScheme"/>.
/// </summary>
/// <remarks>
/// Общий хелпер используется и контрактной моделью карточки (для отображения),
/// и backend при формировании абсолютного адреса для проверки доступности.
/// </remarks>
public static class EnumExtensions
{
    /// <summary>
    /// Возвращает текстовое представление схемы URL: "http://", "https://" или пустую строку.
    /// </summary>
    public static string ToSchemeText(this UrlScheme scheme) => scheme switch
    {
        UrlScheme.Http => "http://",
        UrlScheme.Https => "https://",
        _ => string.Empty
    };
    

    /// <summary>
    /// Возвращает строковое представление состояния сервиса на русском языке.
    /// </summary>
    public static  string ToStatusText(this HealthCheckStatus.HealthStatus status) => status switch
    {
        HealthCheckStatus.HealthStatus.Online => "Онлайн",
        HealthCheckStatus.HealthStatus.Offline => "Оффлайн",
        HealthCheckStatus.HealthStatus.Unknown => "Неизвестно",
        _ => throw new ArgumentOutOfRangeException()
    };
}
