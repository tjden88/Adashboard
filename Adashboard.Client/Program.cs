using System.Text.Json;
using Adashboard.Client.Components;
using Adashboard.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Refit;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// В развёртывании клиент и API живут на одном origin: статику клиента отдаёт Adashboard.Server,
// поэтому адрес API совпадает с базовым адресом хост-документа.
var apiBaseAddress = builder.HostEnvironment.BaseAddress;

// Ответы API разбираются как JSON веб-умолчаний; сообщения об ошибках сервер отдаёт
// телом-текстом, поэтому их читает не сериализатор, а IApiResponse (см. ErrorText).
var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web))
};

builder.Services.AddTransient<NetworkErrorHandler>();

// Базовый адрес API хранится в одном месте: из него строится HTTP-клиент и через него
// относительные пути загрузок превращаются в абсолютные адреса (см. DashboardApiOptions).
var apiOptions = new DashboardApiOptions(apiBaseAddress);
builder.Services.AddSingleton(apiOptions);

// Прикладные сервисы клиента.
// Состояние приложения отдаём как корневое каскадное значение: любой компонент получает его
// через [CascadingParameter], а об изменениях подписчиков уведомляет источник каскада.
builder.Services.AddScoped<AppStateService>();
builder.Services.AddCascadingValue(sp => sp.GetRequiredService<AppStateService>().Source);
builder.Services.AddScoped<HealthCheckService>();

builder.Services
    .AddRefitGeneratedClient<IDashboardApi>(refitSettings)
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiOptions.BaseAddress))
    .AddHttpMessageHandler<NetworkErrorHandler>();

await builder.Build().RunAsync();
