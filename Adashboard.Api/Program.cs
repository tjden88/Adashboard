using Adashboard.Api.Data;
using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();

// OpenAPI-документ отдаём только в среде разработки.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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
