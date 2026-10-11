using Adashboard.Api.Data;
using Adashboard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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

// Подключаем контроллеры и генерацию OpenAPI-документа.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// База данных хранится в едином каталоге данных, который задаётся DASHBOARD_DATA_PATH.
var storage = DashboardStorage.Resolve(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(storage);

// Каталог, заданный через DASHBOARD_DATA_PATH, может быть ещё не создан
// (например, пустой внешний каталог): SQLite не создаёт его сам.
Directory.CreateDirectory(Path.GetDirectoryName(storage.DatabasePath)!);

builder.Services.AddDbContextFactory<DashboardDb>(options =>
    options.UseSqlite($"Data Source={storage.DatabasePath}"));
builder.Services.AddScoped<DashboardLayoutService>();
builder.Services.AddScoped<ImageIconService>();

// Клиент загрузки иконок по ссылке отключён от редиректов: это снижает риск SSRF
// через перенаправление.
builder.Services.AddHttpClient("IconDownloader")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

// Клиент серверной проверки карточек и получения заголовка страницы. Редиректы не следуем,
// чтобы вернуть настоящий код ответа целевого сервиса.
builder.Services.AddHttpClient("Probe")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

builder.Services.AddScoped<ProbeService>();

// Разрешаем кросс-доменные запросы с любого origin: это личный дашборд,
// фронтенд и backend могут быть запущены на разных портах или хостах.
const string CorsPolicyName = "AllowAll";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
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

// При выносе загрузок во внешний каталог он может быть пустым, поэтому поставляемые
// фон и логотип копируются из wwwroot, если их ещё нет.
DashboardUploadsInitializer.Initialize(storage.UploadsPath, app.Environment.WebRootPath, app.Logger);

// Раздаём каталог пользовательских загрузок (фоны, логотип, иконки карточек).
Directory.CreateDirectory(storage.UploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storage.UploadsPath),
    RequestPath = "/uploads"
});

app.UseCors(CorsPolicyName);
app.UseAuthorization();

app.MapControllers();

// Применяем миграции и стартовое наполнение при запуске, как в легаси-проекте.
if (app.Environment.IsDevelopment())
{
    await DebugDbInitializer.InitializeAsync(app.Services);
}
else
{
    await ProductionDbInitializer.InitializeAsync(app.Services);
}

app.Run();
