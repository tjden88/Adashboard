using System.Security.Cryptography;
using System.Text;

namespace Adashboard.Server.Auth;

/// <summary>
/// Ключи доступа к API из конфигурации сервера: основной ключ и ключ расширения браузера.
/// Пустое значение основного ключа отключает защиту.
/// </summary>
public sealed class ApiKeySettings
{
    /// <summary>
    /// Создаёт настройки защиты на основе значений из конфигурации.
    /// </summary>
    public ApiKeySettings(string? key, string? extensionKey)
    {
        Key = key?.Trim() ?? string.Empty;
        ExtensionKey = extensionKey?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Заданный основной ключ доступа или пустая строка, если защита выключена.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Заданный ключ расширения браузера или пустая строка, если он не настроен.
    /// </summary>
    public string ExtensionKey { get; }

    /// <summary>
    /// Требует ли сервер ключ доступа. Определяется только основным ключом: ключ расширения защиту не включает.
    /// </summary>
    public bool IsEnabled => Key.Length > 0;

    /// <summary>
    /// Проверяет, совпадает ли предъявленный ключ с основным или с ключом расширения.
    /// </summary>
    public bool Matches(string? provided)
    {
        if (!IsEnabled || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        // Сравнение за постоянное время: иначе ключ можно было бы подобрать посимвольно по времени ответа.
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Key), providedBytes))
        {
            return true;
        }

        return ExtensionKey.Length > 0 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(ExtensionKey), providedBytes);
    }
}
