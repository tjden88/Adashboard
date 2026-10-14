using Adashboard.Client.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Refit;
using System.Text.Json;
using Adashboard.Client.Services;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var configuredApiBaseUrl = builder.Configuration["ApiBaseUrl"];

var apiBaseUrl = string.IsNullOrWhiteSpace(configuredApiBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : configuredApiBaseUrl;
builder.Services.AddSingleton(new DashboardApiOptions(apiBaseUrl));

var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web))
};

builder.Services
    .AddRefitGeneratedClient<IDashboardApi>(refitSettings)
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiBaseUrl));

builder.Services.AddScoped<HealthCheckService>();
builder.Services.AddScoped<PageTitleService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<EditModeService>();
builder.Services.AddScoped<SiteSettingsService>();

await builder.Build().RunAsync();
