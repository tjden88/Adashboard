using Adashboard.Client.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Refit;
using System.Text.Json;
using Adashboard.Client.Services;

// Точка входа Blazor WebAssembly: приложение стартует в браузере, а данные получает
// по HTTP у отдельного Adashboard.Api. Хост-документ — wwwroot/index.html.
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Корневой компонент-маршрутизатор монтируется в элемент #app из index.html.
builder.RootComponents.Add<Routes>("#app");

// HeadOutlet позволяет компонентам управлять <head> (например, <PageTitle>).
builder.RootComponents.Add<HeadOutlet>("head::after");

// Базовый адрес API берём из конфигурации клиента (wwwroot/appsettings.json). Если там пусто,
// используем адрес текущего хоста: когда клиент раздаёт API, это тот же origin, и запросы
// автоматически идут на него (не нужно зашивать хост и порт).
var configuredApiBaseUrl = builder.Configuration["ApiBaseUrl"];
var apiBaseUrl = string.IsNullOrWhiteSpace(configuredApiBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : configuredApiBaseUrl;
builder.Services.AddSingleton(new DashboardApiOptions(apiBaseUrl));

// Refit-клиент к API dashboard: контракт описан атрибутами в IDashboardApi,
// а реализацию Refit генерирует во время сборки. Регистрируем именно generated-клиент
// (AddRefitGeneratedClient), а не AddRefitClient: второй работает через reflection-билдер
// из отдельного пакета Refit.Reflection, который несовместим с обрезкой IL (PublishTrimmed).
//
// Важно: по умолчанию Refit пишет enum-ы строками, а API ожидает числа (как раньше
// при работе через HttpClientJsonExtensions). Поэтому задаём веб-настройки System.Text.Json:
// camelCase для имён свойств, регистронезависимое чтение и enum-ы числами.
var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
};

builder.Services
    .AddRefitGeneratedClient<IDashboardApi>(refitSettings)
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiBaseUrl));

// Проверка статуса карточек и получение заголовка страницы тоже идут через API
// (целевые сервисы могут не отдавать CORS-заголовки), поэтому отдельный HttpClient
// с прямыми запросами к целевым сайтам больше не нужен.
builder.Services.AddScoped<HealthCheckService>();
builder.Services.AddScoped<PageTitleService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<EditModeService>();

await builder.Build().RunAsync();
