using Adashboard.Components;
using Adashboard.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Точка входа Blazor WebAssembly: приложение стартует в браузере, а данные получает
// по HTTP у отдельного Adashboard.Api. Хост-документ — wwwroot/index.html.
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Корневой компонент-маршрутизатор монтируется в элемент #app из index.html.
builder.RootComponents.Add<Routes>("#app");

// HeadOutlet позволяет компонентам управлять <head> (например, <PageTitle>).
builder.RootComponents.Add<HeadOutlet>("head::after");

// Базовый адрес API берём из конфигурации клиента (wwwroot/appsettings.json).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5024";
builder.Services.AddSingleton(new DashboardApiOptions(apiBaseUrl));

// Общий HttpClient с базовым адресом API: относительные пути сервисов идут к API,
// а абсолютные (health-check, заголовок страницы) — напрямую к целевым сайтам.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

builder.Services.AddScoped<IDashboardLayoutService, DashboardLayoutHttpService>();
builder.Services.AddScoped<IImageIconService, ImageIconHttpService>();
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();
builder.Services.AddScoped<IPageTitleService, PageTitleService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<EditModeService>();

await builder.Build().RunAsync();
