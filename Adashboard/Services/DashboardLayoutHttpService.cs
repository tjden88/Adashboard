using System.Net.Http.Json;
using Adashboard.Domain;

namespace Adashboard.Services;

/// <summary>
/// Клиентская реализация <see cref="IDashboardLayoutService"/>: читает и сохраняет
/// раскладку через эндпоинты <c>/api/layout</c> сервиса Adashboard.Api.
/// </summary>
/// <remarks>
/// Сериализация JSON идёт веб-настройками по умолчанию: свойства в camelCase,
/// enum <see cref="CardStatusMode"/> — числом (совместимо с форматом API).
/// </remarks>
public sealed class DashboardLayoutHttpService(
    HttpClient httpClient,
    ILogger<DashboardLayoutHttpService> logger) : IDashboardLayoutService
{
    /// <summary>
    /// Загружает основную раскладку dashboard через API.
    /// </summary>
    public async Task<DashboardLayout> GetMainLayoutAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начата загрузка раскладки dashboard через API.");

        var layout = await httpClient.GetFromJsonAsync<DashboardLayout>("api/layout", cancellationToken);
        if (layout is null)
        {
            logger.LogWarning("API вернуло пустую раскладку dashboard. Используется раскладка по умолчанию.");
            return new DashboardLayout { Id = 1, ThemeMode = "auto" };
        }

        logger.LogInformation("Раскладка dashboard загружена через API. Категорий: {CategoryCount}.", layout.Categories.Count);
        return layout;
    }

    /// <summary>
    /// Сохраняет основную раскладку dashboard через API.
    /// </summary>
    public async Task SaveMainLayoutAsync(DashboardLayout layout, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато сохранение раскладки dashboard через API. Идентификатор: {LayoutId}.", layout.Id);

        using var response = await httpClient.PutAsJsonAsync("api/layout", layout, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Сохранение раскладки dashboard через API завершилось неудачно. Код: {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException($"Не удалось сохранить раскладку (код {(int)response.StatusCode}).");
        }

        logger.LogInformation("Раскладка dashboard сохранена через API. Категорий: {CategoryCount}.", layout.Categories.Count);
    }
}
