using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Adashboard.Data;

/// <summary>
/// Пути хранения изменяемых данных dashboard: файла базы данных и каталога пользовательских загрузок.
/// </summary>
/// <remarks>
/// Переменная окружения <c>DASHBOARD_DATA_PATH</c> задаёт единый корневой каталог данных
/// (в Docker он совпадает с точкой монтирования volume). Без неё используется раскладка разработки.
/// </remarks>
public sealed record DashboardStorage(string DatabasePath, string UploadsPath)
{
    /// <summary>
    /// Определяет пути хранения по конфигурации и окружению приложения.
    /// </summary>
    /// <param name="configuration">Конфигурация приложения, включая переменные окружения.</param>
    /// <param name="environment">Окружение хостинга для резервных путей разработки.</param>
    /// <returns>Рассчитанные пути хранения.</returns>
    public static DashboardStorage Resolve(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var dataPath = configuration["DASHBOARD_DATA_PATH"];
        if (!string.IsNullOrWhiteSpace(dataPath))
        {
            var root = Path.GetFullPath(dataPath);
            return new DashboardStorage(
                Path.Combine(root, "adashboard.db"),
                Path.Combine(root, "uploads"));
        }

        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        return new DashboardStorage(
            Path.Combine(environment.ContentRootPath, "adashboard.db"),
            Path.Combine(webRoot, "uploads"));
    }
}
