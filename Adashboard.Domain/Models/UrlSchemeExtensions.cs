namespace Adashboard.Domain.Models;

/// <summary>
/// Вспомогательные методы для <see cref="UrlScheme"/>.
/// </summary>
/// <remarks>
/// Общий хелпер используется и контрактной моделью карточки (для отображения),
/// и backend при формировании абсолютного адреса для проверки доступности.
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
