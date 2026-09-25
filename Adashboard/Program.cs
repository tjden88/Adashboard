using Adashboard.Data;
using Adashboard.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Adashboard.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss ";
    options.SingleLine = true;
});

// Уровень логов сервисов приложения задаётся переменной окружения LOG_LEVEL (по умолчанию Warning).
// Уровни Microsoft и EF Core остаются под управлением appsettings.json.
builder.Logging.AddFilter("Adashboard",
    Enum.TryParse<LogLevel>(builder.Configuration["LOG_LEVEL"], ignoreCase: true, out var configuredLogLevel)
        ? configuredLogLevel
        : LogLevel.Warning);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// База данных и пользовательские загрузки хранятся в едином каталоге данных,
// который в Docker совпадает с точкой монтирования volume.
var storage = DashboardStorage.Resolve(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(storage);

builder.Services.AddDbContextFactory<DashboardDbContext>(options =>
    options.UseSqlite($"Data Source={storage.DatabasePath}"));
builder.Services.AddScoped<IDashboardLayoutService, DashboardLayoutService>();
builder.Services.AddScoped<IImageIconService, ImageIconService>();
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();
builder.Services.AddScoped<IPageTitleService, PageTitleService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<EditModeService>();

// Клиент для загрузки иконок отключён от редиректов: это снижает риск SSRF через перенаправление.
builder.Services.AddHttpClient("IconDownloader")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

// Клиент проверки статуса обращается к домашним сервисам, поэтому редиректы (например, на форму входа) отслеживаются.
builder.Services.AddHttpClient("HealthCheck");

// Клиент получения заголовка страницы так же обращается к домашним сервисам и отслеживает редиректы.
builder.Services.AddHttpClient("PageTitle");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

// MapStaticAssets отдаёт только известные на момент сборки файлы, поэтому отдельно
// раздаём каталог пользовательских загрузок, который может быть вынесен в volume.
DashboardUploadsInitializer.Initialize(
    storage.UploadsPath,
    app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
    app.Logger);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storage.UploadsPath),
    RequestPath = "/uploads"
});
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (app.Environment.IsDevelopment())
{
    await DebugDbInitializer.InitializeAsync(app.Services);
}
else
{
    await ProductionDbInitializer.InitializeAsync(app.Services);
}

app.Run();
