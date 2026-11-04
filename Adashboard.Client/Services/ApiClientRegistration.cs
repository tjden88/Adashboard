using Adashboard.Client.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Adashboard.Client.Services;

/// <summary>
/// Регистрирует сгенерированные Refit-клиенты API: каждый интерфейс контроллера получает
/// собственный HTTP-клиент с общим базовым адресом, настройками сериализации и обработчиками.
/// </summary>
public static class ApiClientRegistration
{
    /// <summary>
    /// Добавляет все клиентские интерфейсы API. Добавляя контроллер на сервере и перегенерируя
    /// клиенты (scripts/generate-api-clients.ps1), регистрируй новый интерфейс здесь.
    /// </summary>
    public static IServiceCollection AddDashboardApiClients(this IServiceCollection services, RefitSettings refitSettings, string baseAddress)
    {
        AddClient<ICardsClient>(services, refitSettings, baseAddress);
        AddClient<ICategoriesClient>(services, refitSettings, baseAddress);
        AddClient<IPageClient>(services, refitSettings, baseAddress);
        AddClient<IProbeClient>(services, refitSettings, baseAddress);
        AddClient<ISettingsClient>(services, refitSettings, baseAddress);
        AddClient<IHealthClient>(services, refitSettings, baseAddress);

        return services;
    }

    private static void AddClient<T>(IServiceCollection services, RefitSettings refitSettings, string baseAddress) where T : class
    {
        // Generated-вариант: Refit использует реализации, которые создаёт собственный source generator.
        // Обычный AddRefitClient требует reflection-построитель запросов (пакет Refit.Reflection),
        // которого в WASM-клиенте нет.
        services.AddRefitGeneratedClient<T>(refitSettings)
            .ConfigureHttpClient(client => client.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<NetworkErrorHandler>()
            .AddHttpMessageHandler<ApiKeyHandler>();
    }
}
