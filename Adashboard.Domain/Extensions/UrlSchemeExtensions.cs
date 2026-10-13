using Adashboard.Domain.Enums;

namespace Adashboard.Domain.Extensions;

/// <summary>
/// Вспомогательные методы для <see cref="UrlScheme"/>.
/// </summary>
/// <remarks>
/// Хелпер используется backend при формировании абсолютного адреса сервиса
/// для проверки доступности карточки.
/// </remarks>
public static class UrlSchemeExtensions
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
}
