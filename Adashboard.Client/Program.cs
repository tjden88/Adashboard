using System.Text.Json;
using Adashboard.Client.Components;
using Adashboard.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Refit;

// Базовый адрес API. Пока задан константой (временное решение): при отдельном запуске
// клиента на dev-сервере он указывает на Adashboard.Server. В развёртывании клиент и API
// живут на одном origin, поэтому позже адрес нужно брать из HostEnvironment.BaseAddress.
const string apiBaseAddress = "http://localhost:5024";

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Ответы API разбираются как JSON веб-умолчаний; сообщения об ошибках сервер отдаёт
// телом-текстом, поэтому их читает не сериализатор, а IApiResponse (см. ErrorText).
var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web))
};

builder.Services.AddTransient<NetworkErrorHandler>();

// Прикладные сервисы клиента.
// Состояние приложения отдаём как корневое каскадное значение: любой компонент получает его
// через [CascadingParameter], а об изменениях подписчиков уведомляет источник каскада.
builder.Services.AddScoped<AppState>();
builder.Services.AddCascadingValue(sp => sp.GetRequiredService<AppState>().Source);

builder.Services
    .AddRefitGeneratedClient<IDashboardApi>(refitSettings)
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<NetworkErrorHandler>();

await builder.Build().RunAsync();
