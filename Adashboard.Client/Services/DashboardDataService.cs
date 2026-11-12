using Adashboard.Client.Helpers;
using Adashboard.Domain.Contracts;
using Adashboard.Domain.ReadModels;
using Adashboard.Domain.Reference;
using Microsoft.AspNetCore.Components;
using Refit;

namespace Adashboard.Client.Services;

/// <summary>
/// Единственный владелец dashboard-данных на клиенте: снапшот всех страниц с категориями
/// и карточками, загружаемый один раз при старте и хранящийся в памяти до перезагрузки вкладки.
/// </summary>
/// <remarks>
/// Сервис хранит экземпляры <see cref="DashboardPageReadModel"/>, полученные из API, и мутирует
/// их списки: отдельной модели-копии страницы нет. Чтения и изменения dashboard-данных выполняют
/// только через сервис. После изменения список обновляется ответом API, повторного чтения нет;
/// TTL и фоновое обновление не предусмотрены.
/// </remarks>
public sealed class DashboardDataService : IDisposable
{
    private readonly IPageClient _pagesApi;
    private readonly ICategoriesClient _categoriesApi;
    private readonly ICardsClient _cardsApi;
    private readonly ILogger<DashboardDataService> _logger;
    private readonly CascadingValueSource<DashboardDataService> _source;

    // Изменения выполняются по одному: перетаскивание из модуля раскладки и диалоги не должны
    // менять списки одновременно.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Признак того, что рассылка уведомления идёт: повторное уведомление из подписчика
    // вызвало бы рекурсивную цепочку перерисовок.
    private bool _notifyInProgress;

    // Страницы в порядке меню.
    private List<DashboardPageReadModel> _pages = [];

    /// <summary>
    /// Создаёт сервис данных и источник каскадного значения для него.
    /// </summary>
    public DashboardDataService(IPageClient pagesApi, ICategoriesClient categoriesApi, ICardsClient cardsApi, ILogger<DashboardDataService> logger)
    {
        _pagesApi = pagesApi;
        _categoriesApi = categoriesApi;
        _cardsApi = cardsApi;
        _logger = logger;
        _source = new CascadingValueSource<DashboardDataService>(this, isFixed: false);
    }

    /// <summary>
    /// Источник каскадного значения: регистрируется в DI, чтобы сервис доходил до компонентов.
    /// </summary>
    public CascadingValueSource<DashboardDataService> Source => _source;

    /// <summary>
    /// Страницы в порядке меню.
    /// </summary>
    public IReadOnlyList<DashboardPageReadModel> Pages => _pages;

    /// <summary>
    /// Страница, открытая сейчас: её показывают шапка, боковое меню и представление страницы.
    /// </summary>
    public DashboardPageReadModel SelectedPage { get; private set; } = new();

    /// <summary>
    /// Снапшот загружен: навигация между страницами работает без обращений к API.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Идёт первичная загрузка снапшота: индикатор показывать только на ней.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Текст ошибки первичной загрузки; при отсутствии ошибки — null.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Загружает снапшот всех страниц. Повторный вызов при загруженных данных к API не обращается:
    /// снапшот запрашивается снова только после ошибки.
    /// </summary>
    public async Task<string?> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
            return null;

        IsLoading = true;
        Error = null;
        NotifyChanged();

        try
        {
            _logger.LogDebug("Начата загрузка снапшота страниц dashboard.");

            var response = await _pagesApi.GetFullPages(cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content is null)
            {
                _logger.LogWarning("Снапшот страниц dashboard не загружен. Код: {StatusCode}.", (int?)response.StatusCode);
                Error = response.ErrorText();
                return Error;
            }

            // Порядок меню задаёт Order страницы; Id держит страницы с одинаковым порядком
            // в предсказуемом виде.
            _pages = [.. response.Content.OrderBy(x => x.Page.Order).ThenBy(x => x.Page.Id)];
            IsLoaded = true;

            _logger.LogInformation("Снапшот страниц dashboard загружен. Страниц: {Count}.", _pages.Count);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Загрузка снапшота страниц dashboard завершилась ошибкой.");
            Error = "Не удалось загрузить данные dashboard. Проверьте соединение с сервером и попробуйте снова.";
            return Error;
        }
        finally
        {
            IsLoading = false;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Страница по пути из адреса, регистр пути не важен. Возвращает null, если страницы нет.
    /// </summary>
    public DashboardPageReadModel? GetPageByPath(string path)
        => _pages.FirstOrDefault(x => string.Equals(x.Page.Path, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Страница по идентификатору. Возвращает null, если страницы нет.
    /// </summary>
    public DashboardPageReadModel? GetPageById(int id) => _pages.FirstOrDefault(x => x.Page.Id == id);

    /// <summary>
    /// Делает страницу выбранной: её показывают шапка, боковое меню и представление страницы.
    /// </summary>
    public void SelectPage(DashboardPageReadModel? page)
    {
        var selected = page ?? new DashboardPageReadModel();
        if (ReferenceEquals(selected, SelectedPage))
            return;

        SelectedPage = selected;
        NotifyChanged();
    }

    /// <summary>
    /// Категории выбранной страницы в порядке внутри страницы.
    /// </summary>
    public IReadOnlyList<DashboardCategory> GetCategories() => SelectedPage.Categories.OrderBy(x => x.Order).ToList();

    /// <summary>
    /// Карточки выбранной страницы в порядке положения.
    /// </summary>
    public IReadOnlyList<DashboardCard> GetCards() => SelectedPage.Cards.OrderBy(x => x.Position).ToList();

    /// <summary>
    /// Карточки категории выбранной страницы в порядке положения.
    /// </summary>
    public IReadOnlyList<DashboardCard> GetCards(int categoryId) => SelectedPage.Cards.Where(x => x.DashboardCategoryId == categoryId).OrderBy(x => x.Position).ToList();

    /// <summary>
    /// Категории всех страниц в порядке меню: нужны селектору категории, где перенос карточки
    /// возможен на другую страницу.
    /// </summary>
    public IReadOnlyList<DashboardCategory> GetAllCategories() => _pages.SelectMany(x => x.Categories.OrderBy(c => c.Order)).ToList();

    /// <summary>
    /// Порядковый номер новой категории на странице: она добавляется в конец списка.
    /// </summary>
    public int NextCategoryOrder(int pageId)
    {
        var categories = GetPageById(pageId)?.Categories ?? [];
        return categories.Count > 0 ? categories.Max(x => x.Order) + 1 : 0;
    }

    /// <summary>
    /// Положение новой карточки в категории: она добавляется в конец категории.
    /// </summary>
    public int NextCardPosition(int categoryId)
    {
        var page = GetPageByCategoryId(categoryId);
        var cards = page?.Cards.Where(x => x.DashboardCategoryId == categoryId).ToList() ?? [];
        return cards.Count > 0 ? cards.Max(x => x.Position) + 1 : 0;
    }

    // ---------------------------------------------------------------
    // Страницы.
    // ---------------------------------------------------------------

    /// <summary>
    /// Создаёт страницу dashboard: при успехе пустая страница добавляется в конец меню.
    /// </summary>
    public async Task<string?> CreatePageAsync(DashboardPage page, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато создание страницы dashboard {Title}.", page.Title);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _pagesApi.CreatePage(page, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content is null)
                return Failure("Создание страницы", response);

            // Новая страница всегда встаёт в конец меню: порядок существующих при этом не меняется.
            _pages.Add(new DashboardPageReadModel { Page = response.Content });

            _logger.LogInformation("Страница dashboard создана. Идентификатор: {PageId}.", response.Content.Id);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Изменяет информационную часть страницы. Ответ API не содержит тела, поэтому значения
    /// берутся из запроса, а порядок в меню остаётся прежним.
    /// </summary>
    public async Task<string?> SavePageInfoAsync(DashboardPage info, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато изменение информационной части страницы dashboard {PageId}.", info.Id);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _pagesApi.SavePageInfo(info, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failure("Изменение страницы", response);

            var stored = GetPageById(info.Id);
            if (stored is not null)
            {
                stored.Page.Title = info.Title;
                stored.Page.Description = info.Description;
                stored.Page.Path = info.Path;
                stored.Page.Icon = info.Icon;
                stored.Page.IconColor = info.IconColor;
            }

            _logger.LogInformation("Информационная часть страницы dashboard {PageId} обновлена.", info.Id);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Удаляет страницу dashboard: вместе с ней убираются её категории и карточки.
    /// </summary>
    public async Task<string?> DeletePageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато удаление страницы dashboard {PageId}.", pageId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _pagesApi.DeletePage(pageId, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failure("Удаление страницы", response);

            var stored = GetPageById(pageId);
            if (stored is null)
                return null;

            _pages.Remove(stored);
            if (ReferenceEquals(SelectedPage, stored))
                SelectPage(null);

            _logger.LogInformation("Страница dashboard удалена. Идентификатор: {PageId}.", pageId);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Применяет новый порядок страниц в меню. Основная страница всегда первая и получает порядок 0.
    /// </summary>
    public async Task<string?> ReorderPagesAsync(IReadOnlyList<int> orderedPageIds, CancellationToken cancellationToken = default)
    {
        if (orderedPageIds.Count == 0)
            return null;

        _logger.LogDebug("Начата перестановка страниц dashboard: {Count}.", orderedPageIds.Count);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Порядок считаем до запроса, но применяем только после успеха: при отказе данные
            // не должны расходиться с сервером.
            var target = new List<DashboardPageReadModel>(_pages.Count);

            foreach (var id in orderedPageIds)
            {
                var page = GetPageById(id);
                if (page is not null && !target.Contains(page))
                    target.Add(page);
            }

            // Страниц вне присланного списка быть не должно, но если они есть — сохраняем
            // их относительный порядок в конце.
            foreach (var page in _pages)
            {
                if (!target.Contains(page))
                    target.Add(page);
            }

            var response = await _pagesApi.ReorderPages(orderedPageIds, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failure("Перестановка страниц", response);

            var main = target.FirstOrDefault(x => string.IsNullOrEmpty(x.Page.Path));
            if (main is not null)
            {
                target.Remove(main);
                target.Insert(0, main);
            }

            var order = 0;
            foreach (var page in target)
                page.Page.Order = string.IsNullOrEmpty(page.Page.Path) ? 0 : ++order;

            _pages = target;

            _logger.LogInformation("Новый порядок страниц dashboard применён. Страниц в порядке: {Count}.", orderedPageIds.Count);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ---------------------------------------------------------------
    // Категории.
    // ---------------------------------------------------------------

    /// <summary>
    /// Создаёт категорию: при успехе копия ответа добавляется в список страницы, которой
    /// принадлежит категория.
    /// </summary>
    public async Task<string?> CreateCategoryAsync(DashboardCategory category, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато создание категории {Title}.", category.Title);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _categoriesApi.CreateCategory(category, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content is null)
                return Failure("Создание категории", response);

            var created = response.Content;
            GetPageById(created.DashboardPageId)?.Categories.Add(created);

            _logger.LogInformation("Категория dashboard создана. Идентификатор: {CategoryId}.", created.Id);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Изменяет категорию, в том числе переносит её на другую страницу: обновляются обе страницы.
    /// Ответ API не содержит тела, поэтому значения берутся из запроса.
    /// </summary>
    public async Task<string?> SaveCategoryAsync(DashboardCategory category, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато изменение категории {CategoryId}.", category.Id);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _categoriesApi.UpdateCategory(category, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failure("Изменение категории", response);

            var stored = GetCategoryById(category.Id);
            if (stored is null)
                return null;

            var sourcePage = GetPageById(stored.DashboardPageId);
            var targetPage = GetPageById(category.DashboardPageId);

            stored.Title = category.Title;
            stored.Width = category.Width;
            stored.MobileWidth = category.MobileWidth;
            stored.Order = category.Order;

            // Перенос на другую страницу: категория и её карточки переходят в списки целевой.
            if (sourcePage is not null && targetPage is not null && sourcePage != targetPage)
            {
                var cards = sourcePage.Cards.Where(x => x.DashboardCategoryId == stored.Id).ToList();

                sourcePage.Categories.Remove(stored);
                sourcePage.Cards.RemoveAll(x => x.DashboardCategoryId == stored.Id);

                targetPage.Categories.Add(stored);
                targetPage.Cards.AddRange(cards);

                _logger.LogInformation("Категория {CategoryId} перенесена на страницу {PageId}.", stored.Id, targetPage.Page.Id);
            }

            _logger.LogInformation("Категория dashboard {CategoryId} обновлена.", stored.Id);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Применяет новую ширину категории: на мобильном брейкпоинте сохраняется отдельная
    /// мобильная ширина, на остальных — обычная ширина сетки.
    /// </summary>
    public async Task<string?> SetCategoryWidthAsync(int categoryId, int width, bool isMobile, CancellationToken cancellationToken = default)
    {
        var stored = GetCategoryById(categoryId);
        if (stored is null)
            return null;

        // Мобильный ресайз фиксирует отдельную мобильную ширину: обратно в «авто» она
        // возвращается только через диалог категории.
        var newWidth = isMobile ? stored.Width : Math.Clamp(width, DomainConstants.CategoryWidthMin, DomainConstants.CategoryWidthMax);
        var newMobileWidth = isMobile ? Math.Clamp(width, DomainConstants.CategoryWidthMin, DomainConstants.CategoryMobileWidthMax) : stored.MobileWidth;

        if (stored.Width == newWidth && stored.MobileWidth == newMobileWidth)
            return null;

        // Значение применяется только после успеха запроса, поэтому сохраняем копию: запись
        // в списке сервиса до ответа API остаётся прежней.
        var request = new DashboardCategory { Id = stored.Id, DashboardPageId = stored.DashboardPageId, Order = stored.Order, Title = stored.Title, Width = newWidth, MobileWidth = newMobileWidth };
        return await SaveCategoryAsync(request, cancellationToken);
    }

    /// <summary>
    /// Удаляет категорию вместе с её карточками.
    /// </summary>
    public async Task<string?> DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато удаление категории {CategoryId}.", categoryId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _categoriesApi.DeleteCategory(categoryId, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failure("Удаление категории", response);

            var stored = GetCategoryById(categoryId);
            var page = GetPageByCategoryId(categoryId);
            if (stored is null || page is null)
                return null;

            page.Cards.RemoveAll(x => x.DashboardCategoryId == stored.Id);
            page.Categories.Remove(stored);

            _logger.LogInformation("Категория dashboard {CategoryId} удалена.", categoryId);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Дублирует категорию: копия встаёт сразу после исходной, поэтому порядок последующих
    /// категорий страницы сдвигается на одну. Карточки копии приходят вместе с категорией —
    /// сервер сохраняет их положение.
    /// </summary>
    public async Task<string?> DuplicateCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато дублирование категории {CategoryId}.", categoryId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _categoriesApi.DuplicateCategory(categoryId, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content is null)
                return Failure("Дублирование категории", response);

            var copy = response.Content;
            var page = GetPageByCategoryId(categoryId);
            if (page is null)
                return null;

            foreach (var category in page.Categories.Where(x => x.Id != copy.Category.Id && x.Order >= copy.Category.Order))
                category.Order++;

            var sourceIndex = page.Categories.FindIndex(x => x.Id == categoryId);
            page.Categories.Insert(sourceIndex >= 0 ? sourceIndex + 1 : page.Categories.Count, copy.Category);

            page.Cards.AddRange(copy.Cards);

            _logger.LogInformation("Категория dashboard {CategoryId} продублирована. Идентификатор копии: {CopyCategoryId}. Карточек: {CardCount}.", categoryId, copy.Category.Id, copy.Cards.Count);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Применяет новый порядок категорий выбранной страницы, полученный от перетаскивания.
    /// </summary>
    public async Task<string?> ReorderCategoriesAsync(IReadOnlyList<int> orderedCategoryIds, CancellationToken cancellationToken = default)
    {
        if (orderedCategoryIds.Count == 0)
            return null;

        _logger.LogDebug("Начата перестановка категорий страницы: {Count}.", orderedCategoryIds.Count);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Сначала считаем желаемый порядок, не трогая данные: иначе не отличить реальное
            // изменение от перетаскивания на прежнее место.
            var target = new List<(DashboardCategory Category, int Order)>();
            var order = 0;

            foreach (var id in orderedCategoryIds)
            {
                var category = GetCategoryById(id);
                if (category is not null)
                {
                    target.Add((category, order));
                    order++;
                }
            }

            // Категории, которых нет в присланном списке, уходят в конец, сохраняя
            // относительный порядок.
            foreach (var category in SelectedPage.Categories.OrderBy(x => x.Order))
            {
                if (!orderedCategoryIds.Contains(category.Id))
                {
                    target.Add((category, order));
                    order++;
                }
            }

            var changed = target.Where(x => x.Category.Order != x.Order).ToList();
            if (changed.Count == 0)
            {
                _logger.LogDebug("Порядок категорий не изменился: сохранение не требуется.");
                return null;
            }

            string? error = null;

            foreach (var (category, newOrder) in changed)
            {
                // Порядок категории — обычное сохранение записи: отдельного эндпоинта перестановки нет.
                var oldOrder = category.Order;
                category.Order = newOrder;

                var response = await _categoriesApi.UpdateCategory(category, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    // При отказе значение возвращается: локальные данные не должны расходиться с сервером.
                    category.Order = oldOrder;
                    error = error ?? response.ErrorText();
                }
            }

            if (error is null)
                _logger.LogInformation("Новый порядок категорий применён. Изменено категорий: {Count}.", changed.Count);

            NotifyChanged();
            return error;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ---------------------------------------------------------------
    // Карточки.
    // ---------------------------------------------------------------

    /// <summary>
    /// Создаёт карточку: при успехе она добавляется в конец категории, как это делает сервер.
    /// </summary>
    public async Task<string?> CreateCardAsync(SaveCardRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.Card);

        _logger.LogDebug("Начато создание карточки {Title} в категории {CategoryId}.", request.Card.Title, request.Card.DashboardCategoryId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _cardsApi.CreateCard(request, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content is null)
                return Failure("Создание карточки", response);

            var created = response.Content;
            GetPageByCategoryId(created.DashboardCategoryId)?.Cards.Add(created);

            _logger.LogInformation("Карточка dashboard создана. Идентификатор: {CardId}.", created.Id);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Изменяет карточку, в том числе переносит её в категорию другой страницы: положение
    /// в целевой категории считает сервер, поэтому итоговое значение приходит в ответе API.
    /// </summary>
    public async Task<string?> SaveCardAsync(SaveCardRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.Card);

        _logger.LogDebug("Начато изменение карточки {CardId}.", request.Card.Id);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _cardsApi.UpdateCard(request, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content is null)
                return Failure("Изменение карточки", response);

            var updated = response.Content;
            var sourcePage = GetPageByCardId(updated.Id);
            if (sourcePage is null)
                return null;

            var targetPage = GetPageByCategoryId(updated.DashboardCategoryId) ?? sourcePage;

            var index = sourcePage.Cards.FindIndex(x => x.Id == updated.Id);
            if (index >= 0)
                sourcePage.Cards[index] = updated;

            if (sourcePage != targetPage)
            {
                sourcePage.Cards.Remove(updated);
                targetPage.Cards.Add(updated);

                _logger.LogInformation("Карточка {CardId} перенесена в категорию {CategoryId}.", updated.Id, updated.DashboardCategoryId);
            }

            _logger.LogInformation("Карточка dashboard {CardId} обновлена.", updated.Id);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Дублирует карточку: копия встаёт сразу после исходной, поэтому положение последующих
    /// карточ той же категории сдвигается на одну.
    /// </summary>
    public async Task<string?> DuplicateCardAsync(int cardId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато дублирование карточки {CardId}.", cardId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _cardsApi.DuplicateCard(cardId, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content is null)
                return Failure("Дублирование карточки", response);

            var copy = response.Content;
            var page = GetPageByCardId(cardId);
            if (page is null)
                return null;

            foreach (var card in page.Cards.Where(x => x.DashboardCategoryId == copy.DashboardCategoryId && x.Id != copy.Id && x.Position >= copy.Position))
                card.Position++;

            page.Cards.Add(copy);

            _logger.LogInformation("Карточка dashboard {CardId} продублирована. Идентификатор копии: {CopyCardId}.", cardId, copy.Id);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Удаляет карточку.
    /// </summary>
    public async Task<string?> DeleteCardAsync(int cardId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начато удаление карточки {CardId}.", cardId);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var response = await _cardsApi.DeleteCard(cardId, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failure("Удаление карточки", response);

            var page = GetPageByCardId(cardId);
            if (page is null)
                return null;

            page.Cards.RemoveAll(x => x.Id == cardId);

            _logger.LogInformation("Карточка dashboard {CardId} удалена.", cardId);
            NotifyChanged();
            return null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Применяет новый порядок карточек выбранной страницы, полученный от перетаскивания,
    /// в том числе с переносом в другую категорию.
    /// </summary>
    public async Task<string?> ReorderCardsAsync(int sourceCategoryId, int targetCategoryId, IReadOnlyList<int> sourceCardIds, IReadOnlyList<int> targetCardIds, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Начата перестановка карточек: {Count}.", sourceCardIds.Count + targetCardIds.Count);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Сначала считаем итоговое положение, не трогая данные: так видно, что реально
            // изменилось, и перенос на прежнее место не приводит к сохранению.
            var target = new List<(DashboardCard Card, int CategoryId, int Position)>();
            CollectCards(target, sourceCategoryId, sourceCardIds);

            if (sourceCategoryId != targetCategoryId)
                CollectCards(target, targetCategoryId, targetCardIds);

            var changed = target.Where(x => x.Card.DashboardCategoryId != x.CategoryId || x.Card.Position != x.Position).ToList();
            if (changed.Count == 0)
            {
                _logger.LogDebug("Положение карточек не изменилось: сохранение не требуется.");
                return null;
            }

            string? error = null;

            foreach (var (card, categoryId, position) in changed)
            {
                var oldCategoryId = card.DashboardCategoryId;
                var oldPosition = card.Position;

                card.DashboardCategoryId = categoryId;
                card.Position = position;

                var response = await _cardsApi.UpdateCard(new SaveCardRequest { Card = card }, cancellationToken);
                if (!response.IsSuccessStatusCode || response.Content is null)
                {
                    // При отказе значения возвращаются: локальные данные не должны расходиться с сервером.
                    card.DashboardCategoryId = oldCategoryId;
                    card.Position = oldPosition;
                    error = error ?? response.ErrorText();
                    continue;
                }

                // Положение перенесённой карточки в целевой категории считает сервер,
                // поэтому итоговое значение берётся из ответа.
                card.Position = response.Content.Position;
                MoveCardToCategory(card);
            }

            if (error is null)
                _logger.LogInformation("Новый порядок карточек применён. Изменено карточек: {Count}.", changed.Count);

            NotifyChanged();
            return error;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ---------------------------------------------------------------
    // Помощники.
    // ---------------------------------------------------------------

    // Раскладывает идентификаторы контейнера по позициям: список из DOM полностью задаёт
    // и состав, и порядок карточек категории.
    private void CollectCards(List<(DashboardCard Card, int CategoryId, int Position)> target, int categoryId, IReadOnlyList<int> orderedCardIds)
    {
        for (var position = 0; position < orderedCardIds.Count; position++)
        {
            var card = GetCardById(orderedCardIds[position]);
            if (card is not null)
                target.Add((card, categoryId, position));
        }
    }

    // Переносит карточку в списки страницы, которой теперь принадлежит её категория.
    private void MoveCardToCategory(DashboardCard card)
    {
        var sourcePage = GetPageByCardId(card.Id);
        var targetPage = GetPageByCategoryId(card.DashboardCategoryId);
        if (sourcePage is null || targetPage is null || targetPage == sourcePage)
            return;

        sourcePage.Cards.Remove(card);
        targetPage.Cards.Add(card);
    }

    private DashboardCategory? GetCategoryById(int categoryId) => _pages.SelectMany(x => x.Categories).FirstOrDefault(x => x.Id == categoryId);

    private DashboardCard? GetCardById(int cardId) => _pages.SelectMany(x => x.Cards).FirstOrDefault(x => x.Id == cardId);

    private DashboardPageReadModel? GetPageByCategoryId(int categoryId) => _pages.FirstOrDefault(x => x.Categories.Any(c => c.Id == categoryId));

    private DashboardPageReadModel? GetPageByCardId(int cardId) => _pages.FirstOrDefault(x => x.Cards.Any(c => c.Id == cardId));

    // Отказ сервиса — понятный текст ошибки из ответа API: данные при этом не меняются.
    private string? Failure(string action, IApiResponse response)
    {
        _logger.LogWarning("{Action} завершилось ошибкой API. Код: {StatusCode}.", action, (int?)response.StatusCode);
        return response.ErrorText();
    }

    /// <summary>
    /// Рассылает подписчикам каскадного значения уведомление об изменении данных.
    /// </summary>
    private void NotifyChanged()
    {
        // Подписчик может вызывать сервис снова, и уведомление запустит его повторно:
        // рекурсивную цепочку прерываем здесь.
        if (_notifyInProgress)
            return;

        // Уведомление уходит «в никуда»: ошибку доставки подписчикам только логируем,
        // чтобы она не всплыла как необработанное исключение задачи.
        _ = NotifyChangedSafeAsync();
    }

    private async Task NotifyChangedSafeAsync()
    {
        _notifyInProgress = true;

        try
        {
            await _source.NotifyChangedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось уведомить подписчиков об изменении данных dashboard.");
        }
        finally
        {
            _notifyInProgress = false;
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }
}
