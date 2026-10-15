namespace Adashboard.Client.Services;

/// <summary>
/// Настройки подключения клиента к API dashboard: базовый адрес и преобразование относительных путей загрузок.
/// </summary>
/// <remarks>
/// Относительные пути загрузок (<c>/uploads/...</c>), которые возвращает API, нужно превращать в
/// абсолютные адреса API: в режиме разработки клиент и API живут на разных портах, поэтому «сырой»
/// путь открывался бы на стороне клиента и картинки не загружались бы.
/// </remarks>
public sealed class DashboardApiOptions
{
    /// <summary>
    /// Создаёт настройки с базовым адресом API без завершающего слэша.
    /// </summary>
    public DashboardApiOptions(string baseAddress)
    {
        BaseAddress = baseAddress.TrimEnd('/');
    }

    /// <summary>
    /// Базовый адрес API без завершающего слэша.
    /// </summary>
    public string BaseAddress { get; }

    /// <summary>
    /// Преобразует относительный путь загрузки в абсолютный адрес для использования в разметке.
    /// </summary>
    public string? ResolveUploadPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        // Абсолютные ссылки и data URL уже готовы к отображению.
        if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var relative = path.StartsWith('/') ? path[1..] : path;
        return $"{BaseAddress}/{relative}";
    }
}
