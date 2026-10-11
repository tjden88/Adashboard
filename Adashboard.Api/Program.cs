using Adashboard.Api.Data;
using Adashboard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss ";
    options.SingleLine = true;
});

var services = builder.Services;

// Единый каталог изменяемых данных: файл БД и пользовательские загрузки. Лежит вне wwwroot,
// поэтому сама БД не отдаётся как статика. В контейнере каталог монтируется как volume.
var dataPath = Path.Combine(builder.Environment.ContentRootPath, "appdata");
var paths = new DashboardPaths(
    DataPath: dataPath,
    DatabasePath: Path.Combine(dataPath, "data.db"),
    UploadsPath: Path.Combine(dataPath, "uploads"));

Directory.CreateDirectory(paths.UploadsPath);
services.AddSingleton(paths);

// Подключаем контроллеры и генерацию OpenAPI-документа.

services.AddControllers();
services.AddOpenApi();

services.AddDbContext<DashboardDb>(options => options.UseSqlite($"Data Source={paths.DatabasePath}"));
services.AddScoped<DbInitializer>();
services.AddScoped<AssetsInitializer>();

services.AddScoped<DashboardLayoutService>();
services.AddScoped<ImageIconService>();
services.AddScoped<ProbeService>();

// Клиент загрузки иконок по ссылке отключён от редиректов: это снижает риск SSRF
// через перенаправление.
services.AddHttpClient("IconDownloader")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

// Клиент серверной проверки карточек и получения заголовка страницы. Редиректы не следуем,
// чтобы вернуть настоящий код ответа целевого сервиса.
services.AddHttpClient("Probe")
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
