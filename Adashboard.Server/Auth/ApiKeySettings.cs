using System.Security.Cryptography;
using System.Text;

namespace Adashboard.Server.Auth;

/// <summary>
/// Ключ доступа к API из конфигурации сервера: пустое значение отключает защиту.
/// </summary>
public sealed class ApiKeySettings
{
    /// <summary>
    /// Создаёт настройки защиты на основе значения из конфигурации.
    /// </summary>
    public ApiKeySettings(string? key) => Key = key?.Trim() ?? string.Empty;

    /// <summary>
    /// Заданный ключ доступа или пустая строка, если защита выключена.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Требует ли сервер ключ доступа.
    /// </summary>
    public bool IsEnabled => Key.Length > 0;

    /// <summary>
    /// Проверяет, совпадает ли предъявленный ключ с заданным.
    /// </summary>
    public bool Matches(string? provided)
    {
        if (!IsEnabled || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        // Сравнение за постоянное время: иначе ключ можно было бы подобрать посимвольно по времени ответа.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Key),
            Encoding.UTF8.GetBytes(provided));
    }
}
