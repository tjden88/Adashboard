namespace Adashboard.Client.Services;

/// <summary>
/// Настройки подключения WASM-клиента к API dashboard.
/// </summary>
/// <remarks>
/// Используется, чтобы относительные пути загрузок (например, <c>/uploads/card-icons/x.png</c>),
/// которые возвращает API, превращать в абсолютные адреса API при отображении в браузере.
/// </remarks>
public sealed record DashboardApiOptions
{
    /// <summary>
    /// Создаёт настройки с базовым адресом API без завершающего слэша.
    /// </summary>
    /// <param name="baseAddress">Базовый адрес API, например <c>http://localhost:5024</c>.</param>
    public DashboardApiOptions(string baseAddress)
    {
        BaseAddress = baseAddress.TrimEnd('/');
    }

    /// <summary>
    /// Базовый адрес API без завершающего слэша.
    /// </summary>
    public string BaseAddress { get; }

    /// <summary>
    /// Преобразует относительный путь загрузки в абсолютный адрес API.
    /// </summary>
    /// <param name="path">Относительный путь загрузки или уже абсолютный адрес либо data URL.</param>
    /// <returns>Абсолютный адрес для использования в разметке, либо исходное значение.</returns>
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
