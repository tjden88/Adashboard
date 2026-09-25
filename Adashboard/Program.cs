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

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "adashboard.db");

builder.Services.AddDbContextFactory<DashboardDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddScoped<IDashboardLayoutService, DashboardLayoutService>();
builder.Services.AddScoped<IImageIconService, ImageIconService>();
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

// MapStaticAssets отдаёт только известные на момент сборки файлы, поэтому отдельно
// раздаём каталог с пользовательскими иконками, созданными во время работы.
var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
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
