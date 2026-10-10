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

// База данных хранится в едином каталоге данных, который в Docker совпадает
// с точкой монтирования volume.
var storage = DashboardStorage.Resolve(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(storage);

// Каталог, заданный через DASHBOARD_DATA_PATH, может быть ещё не создан
// (например, пустой volume): SQLite не создаёт его сам.
Directory.CreateDirectory(Path.GetDirectoryName(storage.DatabasePath)!);

builder.Services.AddDbContextFactory<DashboardDbContext>(options =>
    options.UseSqlite($"Data Source={storage.DatabasePath}"));
builder.Services.AddScoped<IDashboardLayoutService, DashboardLayoutService>();
builder.Services.AddScoped<IImageIconService, ImageIconService>();

// Клиент загрузки иконок по ссылке отключён от редиректов: это снижает риск SSRF
// через перенаправление.
builder.Services.AddHttpClient("IconDownloader")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

// Разрешаем запросы standalone WASM-клиента. Список origin задаётся конфигурацией,
// а в Development по умолчанию разрешаем локальный origin фронтенда.
const string CorsPolicyName = "WasmClient";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
{
    allowedOrigins = ["http://localhost:5173"];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
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

// При выносе загрузок в volume каталог может быть пустым, поэтому поставляемые
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
