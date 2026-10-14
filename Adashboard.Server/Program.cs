using Adashboard.Data;
using Adashboard.Server.Mapping;
using Adashboard.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Глобальная конфигурация Mapster: правила маппинга «сущность БД ↔ контрактная модель».
MapperConfig.Configure();

builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss ";
    options.SingleLine = true;
});

var services = builder.Services;


var paths = new DashboardPaths(builder.Environment.ContentRootPath);
services.AddSingleton(paths);

// Подключаем контроллеры и генерацию OpenAPI-документа.

services.AddControllers();
services.AddOpenApi();

services.AddDbContext<DashboardDb>(options => options.UseSqlite($"Data Source={paths.DatabasePath}"));
services.AddScoped<DbInitializer>();
services.AddScoped<AssetsInitializer>();

services.AddScoped<LayoutService>();
services.AddScoped<SiteSettingsService>();

// Сервис хранения изображений-иконок карточек: пишет файлы в каталог загрузок.
services.AddScoped<ImageIconService>();

// Клиент серверной проверки карточек и получения заголовка страницы. Редиректы не следуем,
// чтобы вернуть настоящий код ответа целевого сервиса.
services.AddHttpClient<ProbeService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });


// Разрешаем кросс-доменные запросы с любого origin: это личный дашборд,
// фронтенд и backend могут быть запущены на разных портах или хостах.
const string corsPolicyName = "AllowAll";
services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// OpenAPI-документ и интерактивную документацию отдаём только в среде разработки.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Scalar — современная замена Swagger UI: интерактивная веб-морда для проверки
    // и описания API, построенная на сгенерированном OpenAPI-документе.
    app.MapScalarApiReference();
}


// Отдаём WASM-клиент: UseBlazorFrameworkFiles обслуживает _framework (blazor.webassembly.js,
// dotnet.js и т.п.), UseStaticFiles — статику клиента из static web assets (index.html, app.css, lib...).
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Раздаём каталог пользовательских загрузок. Провайдер наведён только на uploads,
// поэтому файл БД из каталога данных наружу не отдаётся.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(paths.UploadsPath),
    RequestPath = "/uploads"
});

app.UseCors(corsPolicyName);
app.UseAuthorization();

app.MapControllers();

// Клиент — SPA: неизвестные пути отдаём как index.html, чтобы работал клиентский роутинг.
app.MapFallbackToFile("index.html");

// Применяем миграции и стартовое наполнение при запуске.
await using var scope = app.Services.CreateAsyncScope();
var sp = scope.ServiceProvider;
await sp.GetRequiredService<DbInitializer>().InitializeAsync();
sp.GetRequiredService<AssetsInitializer>().Initialize();

app.Run();
