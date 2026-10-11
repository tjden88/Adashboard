using Adashboard.Api.Data;
using Adashboard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var dataPath = Path.Combine(Environment.CurrentDirectory, "data");
Directory.CreateDirectory(dataPath);

// Настройка логирования повторяет легаси-проект: единый формат консоли и уровень
// логов приложения по переменной окружения LOG_LEVEL (по умолчанию Warning).
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss ";
    options.SingleLine = true;
});

builder.Logging.AddFilter("Adashboard",
    Enum.TryParse<LogLevel>(builder.Configuration["LOG_LEVEL"], ignoreCase: true, out var configuredLogLevel)
        ? configuredLogLevel
        : LogLevel.Warning);


var services = builder.Services;
// Подключаем контроллеры и генерацию OpenAPI-документа.

services.AddControllers();
services.AddOpenApi();

services.AddDbContext<DashboardDb>(options => options.UseSqlite($"Data Source={Path.Combine(dataPath, "data.db")}"));
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


app.UseCors(corsPolicyName);
app.UseAuthorization();

app.MapControllers();

// Применяем миграции и стартовое наполнение при запуске.
await using var scope = app.Services.CreateAsyncScope();
var sp = scope.ServiceProvider;
await sp.GetRequiredService<DbInitializer>().InitializeAsync();
sp.GetRequiredService<AssetsInitializer>().Initialize();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(dataPath, "uploads")),
    RequestPath = "/uploads"
});

app.Run();
