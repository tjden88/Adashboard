using System.Text.RegularExpressions;
using Adashboard.Data;
using Adashboard.Domain.Reference;
using Adashboard.Server.Auth;
using Adashboard.Server.Mapping;
using Adashboard.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
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

// Защита API ключом доступа. Ключ задаётся в конфигурации (ApiKey); пустое значение
// отключает защиту — так локальная разработка и генерация OpenAPI-документа не требуют ключа.
// Секрет держим вне репозитория: в переменной окружения ApiKey или в пользовательских секретах.
var apiKeySettings = new ApiKeySettings(builder.Configuration["ApiKey"]);
services.AddSingleton(apiKeySettings);
services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
services.AddAuthorization();

// Подключаем контроллеры и генерацию OpenAPI-документа.

services.AddControllers();

// Контракт ошибок API: любой отказ отдаётся кодом 400/404 и текстом ошибки в теле
// (text/plain), без обёрток вроде { error: ... }. Автоматический 400 от валидации
// модели приводит к тому же виду, чтобы клиент разбирал один формат и не показывал
// в UI технические детали разбора запроса.
services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = _ => new ContentResult
    {
        StatusCode = StatusCodes.Status400BadRequest,
        ContentType = "text/plain; charset=utf-8",
        Content = "Некорректный запрос."
    };
});

services.AddOpenApi();

services.AddDbContext<DashboardDb>(options => options.UseSqlite($"Data Source={paths.DatabasePath}"));
services.AddScoped<DbInitializer>();
services.AddScoped<AssetsInitializer>();

services.AddScoped<PageService>();
services.AddScoped<AppSettingsService>();

// Сервис хранения изображений-иконок карточек: пишет файлы в каталог загрузок.
services.AddScoped<ImageIconService>();

// Клиент серверной проверки карточек и получения заголовка страницы. Редиректы не следуем,
// чтобы вернуть настоящий код ответа целевого сервиса. Представляемся понятным User-Agent:
// без него многие сайты отвечают 403 и заголовок или иконку получить не удаётся.
services.AddHttpClient<ProbeService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    })
    .ConfigureHttpClient(client => client.DefaultRequestHeaders.UserAgent.ParseAdd("Adashboard/1.0"));


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
    // Прокси отладки Blazor WASM: браузер подключается к /_framework/debug/ws-proxy,
    // иначе точки останова в коде клиента недостижимы при запуске через сервер.
    app.UseWebAssemblyDebugging();

    app.MapOpenApi();

    // Scalar — современная замена Swagger UI: интерактивная веб-морда для проверки
    // и описания API, построенная на сгенерированном OpenAPI-документе.
    app.MapScalarApiReference();
}


// Кэширование статики клиента. У файлов Blazor в имени есть хеш сборки (10 символов
// латиницы и цифр), поэтому их содержимое уже никогда не меняется: такие файлы можно
// кэшировать навсегда, и повторные открытия приложения не будут скачивать сборки заново.
// Оболочку же (index.html, service worker) кэшировать нельзя — после обновления браузер
// должен получить новую версию. Работает при любом адресе и протоколе: в отличие от
// service worker, обычному HTTP-кэшу не нужен защищённый контекст.
var fingerprintedAssetPattern = new Regex(@"\.[a-z0-9]{10}\.(?:wasm|js)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

app.Use((context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        return next(context);
    }

    // Путь запоминаем до следующего слоя: для файлов _framework он подменяется
    // на .br/.gz, а решение о кэшировании принимается по исходному имени.
    var requestPath = context.Request.Path;

    // Заголовки ставим в OnStarting: UseBlazorFrameworkFiles выставляет свой no-cache
    // позже, и перекрыть его можно только на старте ответа.
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;

        if (requestPath.StartsWithSegments("/_framework"))
        {
            headers.CacheControl = fingerprintedAssetPattern.IsMatch(requestPath.Value ?? string.Empty)
                ? "public, max-age=31536000, immutable"
                : "no-cache";
        }
        else if (requestPath.StartsWithSegments(DomainConstants.UploadsUrl + "/card-icons"))
        {
            // Имя файла содержит хеш содержимого (см. ImageIconService).
            headers.CacheControl = "public, max-age=31536000, immutable";
        }
        else if (requestPath.Value?.Contains("service-worker", StringComparison.OrdinalIgnoreCase) == true
            || (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            headers.CacheControl = "no-cache";
        }

        return Task.CompletedTask;
    });

    return next(context);
});


// Отдаём WASM-клиент: UseBlazorFrameworkFiles обслуживает _framework (blazor.webassembly.js,
// dotnet.js и т.п.), UseStaticFiles — статику клиента из static web assets (index.html, app.css, lib...).
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Раздаём каталог пользовательских загрузок. Провайдер наведён только на uploads,
// поэтому файл БД из каталога данных наружу не отдаётся. Каталог создаём заранее:
// PhysicalFileProvider требует, чтобы он уже существовал (важно, например, при
// генерации OpenAPI на сборке, когда каталог данных ещё пуст).
Directory.CreateDirectory(paths.UploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(paths.UploadsPath),
    RequestPath = DomainConstants.UploadsUrl,
    OnPrepareResponse = context =>
    {
        // Пользовательские загрузки — недоверенные данные. Для SVG, который может содержать
        // скрипты, запрещаем любые внешние и встроенные загрузки: в <img> скрипты не исполняются,
        // но при прямом переходе на файл это защищает от XSS на своём origin.
        var headers = context.Context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";

        if (context.File.Name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
        }
    }
});

app.UseCors(corsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

var controllers = app.MapControllers();

// Ключ задан — закрываем весь API. Статика клиента, пользовательские загрузки и отдача
// index.html остаются открытыми: без них приложение не загрузилось бы в браузере.
if (apiKeySettings.IsEnabled)
{
    controllers.RequireAuthorization();
    app.Logger.LogInformation("Защита API ключом доступа включена.");
}
else
{
    app.Logger.LogWarning("Ключ доступа (ApiKey) не задан: API работает без авторизации.");
}

// Клиент — SPA: неизвестные пути отдаём как index.html, чтобы работал клиентский роутинг.
app.MapFallbackToFile("index.html");

// Применяем миграции и стартовое наполнение при запуске.
await using var scope = app.Services.CreateAsyncScope();
var sp = scope.ServiceProvider;
await sp.GetRequiredService<DbInitializer>().InitializeAsync();
sp.GetRequiredService<AssetsInitializer>().Initialize();

app.Run();
