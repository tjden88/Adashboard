using Adashboard.Domain.Contracts;
using Adashboard.Domain.Reference;
using Microsoft.AspNetCore.Components;

namespace Adashboard.Client.Services;

/// <summary>
/// Состояние текущей страницы dashboard: загруженные данные и статус загрузки.
/// </summary>
public sealed class PageState : IDisposable
{
    private readonly IDashboardApi _api;
    private readonly ILogger<PageState> _logger;
    private readonly CascadingValueSource<PageState> _source;

    // Отмена предыдущей незавершённой загрузки: при быстром переходе её результат не должен
    // перезаписать данные новой страницы.
    private CancellationTokenSource? _loadCancellation;

    // Путь последней загрузки: отличает переход между страницами от повторного срабатывания
    // жизненного цикла с тем же параметром маршрута.
    private string _loadedPath = string.Empty;
    private bool _isLoaded;

    // Номер актуальной загрузки: результат отменённой загрузки не должен перезаписать новую.
    private int _loadVersion;

    // Имя приложения: подставляется вместо пустого заголовка страницы (шапка и вкладка браузера).
    private const string AppTitleName = "Adashboard";

    /// <summary>
    /// Создаёт состояние текущей страницы и источник каскадного значения для него.
    /// </summary>
    public PageState(IDashboardApi api, ILogger<PageState> logger)
    {
        _api = api;
        _logger = logger;
        _source = new CascadingValueSource<PageState>(this, isFixed: false);
    }

    /// <summary>
    /// Источник каскадного значения: регистрируется в DI, чтобы состояние доходило до компонентов.
    /// </summary>
    public CascadingValueSource<PageState> Source => _source;

    /// <summary>
    /// Информационная часть текущей страницы.
    /// </summary>
    public DashboardPage Page { get; private set; } = new();

    /// <summary>
    /// Заголовок dashboard для шапки и вкладки браузера: заголовок текущей страницы,
    /// а при пустом значении — имя приложения.
    /// </summary>
    public string DisplayTitle => string.IsNullOrWhiteSpace(Page.Title) ? AppTitleName : Page.Title;

    /// <summary>
    /// Список всех страниц dashboard: нужен для поиска страницы по пути и выбора страницы в диалогах.
    /// </summary>
    public IReadOnlyList<DashboardPage> Pages { get; private set; } = [];

    /// <summary>
    /// Категории текущей страницы.
    /// </summary>
    public IReadOnlyList<DashboardCategory> Categories { get; private set; } = [];

    /// <summary>
    /// Карточки категорий текущей страницы.
    /// </summary>
    public IReadOnlyList<DashboardCard> Cards { get; private set; } = [];

    /// <summary>
    /// Идёт загрузка данных страницы.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Страница по текущему пути не найдена.
    /// </summary>
    public bool IsNotFound { get; private set; }

    /// <summary>
    /// Текст ошибки загрузки; <c>null</c>, если загрузка прошла без ошибок.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Загружает страницу по пути из адреса, если она ещё не загружена или путь изменился.
    /// </summary>
    /// <param name="path">Путь из адреса; <c>null</c> или пустая строка — основная страница «/».</param>
    public async Task LoadAsync(string? path)
    {
        var normalized = NormalizePath(path);

        // Тот же путь уже загружен: повторный вызов жизненного цикла не должен обращаться к API.
        if (_isLoaded && string.Equals(normalized, _loadedPath, StringComparison.Ordinal))
        {
            return;
        }

        await LoadCoreAsync(normalized);
    }

    /// <summary>
    /// Повторно загружает текущую страницу (кнопка «Повторить» после ошибки).
    /// </summary>
    public Task ReloadAsync() => LoadCoreAsync(_loadedPath);

    /// <summary>
    /// Отменяет незавершённую загрузку при завершении работы сервиса.
    /// </summary>
    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    // Загружает страницу и её содержимое, приводя состояние к одному из итогов:
    // загружено, не найдено или ошибка.
    private async Task LoadCoreAsync(string normalized)
    {
        var version = ++_loadVersion;

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        _loadedPath = normalized;
        _isLoaded = true;
        IsLoading = true;
        IsNotFound = false;
        Error = null;
        NotifyChanged();

        try
        {
            // Регистр пути из адреса не важен, поэтому допустимость проверяем без его учёта:
            // адрес «/About» открывает страницу «about», а невозможный путь сразу даёт 404.
            if (normalized.Length > 0 && !PagePathRules.IsValid(normalized.ToLowerInvariant()))
            {
                ApplyNotFound();
                return;
            }

            _logger.LogDebug("Загрузка страницы dashboard по пути {Path}.", normalized);

            // Список страниц нужен, чтобы по адресу найти нужную, а затем загрузить её содержимое.
            var pagesResponse = await _api.GetPagesAsync(cancellationToken);
            if (!IsCurrent(version))
            {
                return;
            }

            if (!pagesResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Не удалось получить список страниц. Код: {StatusCode}.", (int?)pagesResponse.StatusCode);
                ApplyError(pagesResponse.ErrorText());
                return;
            }

            Pages = pagesResponse.Content?.ToList() ?? [];

            var page = Pages.FirstOrDefault(x => string.Equals(x.Path, normalized, StringComparison.OrdinalIgnoreCase));
            if (page is null)
            {
                ApplyNotFound();
                return;
            }

            var pageResponse = await _api.GetPageAsync(page.Id, cancellationToken);
            if (!IsCurrent(version))
            {
                return;
            }

            if (!pageResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Не удалось загрузить страницу {PageId}. Код: {StatusCode}.", page.Id, (int?)pageResponse.StatusCode);
                ApplyError(pageResponse.ErrorText());
                return;
            }

            // Read-модель отдаёт страницу и плоские списки категорий и карточек.
            var readModel = pageResponse.Content;
            Page = readModel?.Page ?? page;
            Categories = readModel?.Categories ?? [];
            Cards = readModel?.Cards ?? [];

            _logger.LogDebug("Страница dashboard по пути {Path} загружена.", normalized);

            Finish();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Переход на другую страницу отменил загрузку: состояние обновит новая загрузка.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось загрузить страницу dashboard по пути {Path}.", normalized);
            ApplyError("Не удалось загрузить страницу. Проверьте соединение с сервером и попробуйте снова.");
        }
    }

    // Приводит состояние к «страница не найдена» и очищает данные предыдущей страницы.
    private void ApplyNotFound()
    {
        _logger.LogWarning("Страница dashboard по пути {Path} не найдена.", _loadedPath);

        ResetContent();
        IsNotFound = true;
        Finish();
    }

    // Приводит состояние к ошибке загрузки и очищает данные предыдущей страницы.
    private void ApplyError(string message)
    {
        ResetContent();
        Error = message;
        Finish();
    }

    private void ResetContent()
    {
        Page = new DashboardPage();
        Categories = [];
        Cards = [];
    }

    private void Finish()
    {
        IsLoading = false;
        NotifyChanged();
    }

    // Признак того, что загрузка всё ещё актуальна: её мог отменить более новый переход.
    private bool IsCurrent(int version) => version == _loadVersion;

    // Приводит путь из адреса к виду, в котором он хранится: без ведущих и завершающих слэшей.
    private static string NormalizePath(string? path) => path?.Trim().Trim('/') ?? string.Empty;

    // Рассылает подписчикам каскадного значения уведомление об изменении состояния.
    // NotifyChangedAsync только ставит уведомление в очередь рендерера, поэтому результат не ожидаем.
    private void NotifyChanged() => _ = _source.NotifyChangedAsync();
}
