using Adashboard.Domain.Contracts;
using Adashboard.Domain.ReadModels;
using Refit;

namespace Adashboard.Client.Services;

/// <summary>
/// Декларативный REST-клиент к Adashboard.Server, построенный библиотекой Refit.
/// </summary>
/// <remarks>
/// <para>
/// Refit — это библиотека, которая по обычному C#-интерфейсу генерирует реализацию HTTP-клиента.
/// Достаточно описать методы и повесить атрибуты, а код запросов (сборка URL, сериализация тела,
/// десериализация ответа) Refit сгенерирует сам во время компиляции. Поэтому здесь нет ни одного
/// ручного вызова <c>HttpClient</c> — только описание контракта.
/// </para>
/// <para>
/// Базовый адрес API задаётся один раз при регистрации клиента в <c>Program.cs</c> через
/// <c>AddRefitGeneratedClient&lt;IDashboardApi&gt;()</c>. Относительные пути из атрибутов
/// (например, <c>/api/layout</c>) Refit приклеивает к этому базовому адресу.
/// </para>
/// <para>
/// Атрибуты запроса:
/// <list type="bullet">
/// <item><description><c>[Get]</c> — HTTP GET, читает данные. Ответ десериализуется в указанный тип.</description></item>
/// <item><description><c>[Post]</c> — HTTP POST, создаёт ресурс; сервер возвращает созданную модель с назначенным Id.</description></item>
/// <item><description><c>[Put]</c> — HTTP PUT, изменяет существующий ресурс (обычно ответ 204 без тела).</description></item>
/// <item><description><c>[Delete]</c> — HTTP DELETE, удаляет ресурс.</description></item>
/// <item><description><c>[Body]</c> — помечает параметр как тело запроса (по умолчанию JSON).</description></item>
/// <item><description><c>[Multipart]</c> и <c>[AliasAs]</c> — отправка файла как <c>multipart/form-data</c>; <c>[AliasAs]</c> задаёт имя поля формы.</description></item>
/// <item><description><c>[Query]</c> — подставляет значение как параметр строки запроса (<c>?url=...</c>).</description></item>
/// </list>
/// </para>
/// <para>
/// Фигурные скобки в пути (<c>{id}</c>) — это подстановка значений параметров метода в URL.
/// </para>
/// <para>
/// Важно про модели: API разделяет чтение и запись. Агрегат для отображения отдаёт read-модель
/// <see cref="DashboardLayoutReadModel"/> (плоские списки категорий и карточек), а команды создания
/// и изменения принимают плоские контрактные модели <see cref="DashboardCategory"/> и
/// <see cref="DashboardCard"/>.
/// </para>
/// </remarks>
public interface IDashboardApi
{
    /// <summary>
    /// Максимальный размер принимаемого изображения-иконки в байтах.
    /// </summary>
    /// <remarks>
    /// То же ограничение действует на стороне API (проверка перед сохранением файла),
    /// поэтому клиент отсекает слишком большие изображения ещё до отправки.
    /// </remarks>
    const long MaxImageBytes = 2 * 1024 * 1024;

    // ---------------------------------------------------------------------
    // Раскладка (информационная часть: заголовок и описание).
    // ---------------------------------------------------------------------

    /// <summary>
    /// Загружает основную раскладку dashboard (<c>GET /api/layout</c>).
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>
    /// Read-модель с информацией о раскладке и плоскими списками категорий и карточек;
    /// <c>null</c>, если API вернуло пустое тело.
    /// </returns>
    [Get("/api/layout")]
    Task<DashboardLayoutReadModel> GetLayoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет заголовок и описание раскладки (<c>PUT /api/layout/info</c>).
    /// </summary>
    /// <param name="info">Плоская модель раскладки с новыми полями <c>Title</c> и <c>Description</c>.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <remarks>
    /// Ответ 204 без тела: Refit не возвращает результат, а при ошибке (например, 400) выбрасывает
    /// <see cref="ApiException"/>. Категории и карточки этим запросом не затрагиваются.
    /// </remarks>
    [Put("/api/layout/info")]
    Task SaveLayoutInfoAsync([Body] DashboardLayout info, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------------
    // Категории: отдельные эндпоинты на создание, изменение и удаление.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Создаёт категорию (<c>POST /api/categories</c>).
    /// </summary>
    /// <param name="category">Плоская модель категории; идентификатор назначает сервер.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная категория с назначенным <c>Id</c>.</returns>
    [Post("/api/categories")]
    Task<DashboardCategory> CreateCategoryAsync(
        [Body] DashboardCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет категорию (<c>PUT /api/categories</c>).
    /// </summary>
    /// <param name="category">Новые значения полей категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [Put("/api/categories")]
    Task UpdateCategoryAsync(
        [Body] DashboardCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет категорию вместе с её карточками (<c>DELETE /api/categories/{id}</c>).
    /// </summary>
    /// <param name="id">Идентификатор категории.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [Delete("/api/categories/{id}")]
    Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------------
    // Карточки: отдельные эндпоинты на создание, изменение и удаление.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Создаёт карточку в указанной категории (<c>POST /api/cards</c>).
    /// </summary>
    /// <param name="card">Плоская модель карточки, включая <c>DashboardCategoryId</c>.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная карточка с назначенным <c>Id</c>.</returns>
    [Post("/api/cards")]
    Task<DashboardCard> CreateCardAsync(
        [Body] DashboardCard card,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет карточку (<c>PUT /api/cards/{id}</c>).
    /// </summary>
    /// <param name="id">Идентификатор карточки.</param>
    /// <param name="card">Новые значения полей карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [Put("/api/cards/{id}")]
    Task UpdateCardAsync(
        int id,
        [Body] DashboardCard card,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет карточку (<c>DELETE /api/cards/{id}</c>).
    /// </summary>
    /// <param name="id">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [Delete("/api/cards/{id}")]
    Task DeleteCardAsync(int id, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------------
    // Изображения-иконки карточек.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Сохраняет изображение-иконку, загруженное с устройства
    /// (<c>POST /api/cards/{cardId}/image</c>, multipart/form-data, поле <c>file</c>).
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="file">Файл изображения, отправляемый как часть multipart-формы.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Ответ API; путь к изображению читается из тела отдельно.</returns>
    /// <remarks>
    /// Возвращаем <see cref="HttpResponseMessage"/>, чтобы неуспешный ответ (400 с описанием ошибки)
    /// разбирать вручную и показывать пользователю понятное сообщение, а не полагаться на исключение.
    /// </remarks>
    [Multipart]
    [Post("/api/cards/{cardId}/image")]
    Task<HttpResponseMessage> UploadImageAsync(
        int cardId,
        [AliasAs("file")] StreamPart file,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Скачивает изображение по ссылке и сохраняет его для карточки
    /// (<c>POST /api/cards/{cardId}/image/from-url</c>).
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="request">Тело запроса с полем <c>url</c> (анонимный объект <c>new { url }</c>).</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Ответ API; путь к изображению читается из тела отдельно.</returns>
    [Post("/api/cards/{cardId}/image/from-url")]
    Task<HttpResponseMessage> UploadImageFromUrlAsync(
        int cardId,
        [Body] object request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет все сохранённые изображения карточки (<c>DELETE /api/cards/{cardId}/image</c>).
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    [Delete("/api/cards/{cardId}/image")]
    Task DeleteImageAsync(int cardId, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------------
    // Серверные проверки: доступность сервиса и заголовок страницы.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Проверяет доступность сервиса карточки на стороне API (<c>GET /api/probe/health/{cardId}</c>).
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Ответ с состоянием сервиса и пояснением.</returns>
    /// <remarks>
    /// Возвращается read-модель API <see cref="HealthCheckResult"/>: отдельный транспортный DTO не заводим.
    /// API сам находит карточку по идентификатору и обращается к её сервису. Браузерный клиент
    /// не может сделать это напрямую: целевые сервисы не отдают CORS-заголовки.
    /// </remarks>
    [Get("/api/probe/health/{cardId}")]
    Task<HealthCheckResult?> CheckHealthAsync(int cardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Запрашивает заголовок страницы у API (<c>GET /api/probe/title</c>).
    /// </summary>
    /// <param name="url">Абсолютная HTTP(S)-ссылка на страницу.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Ответ с заголовком страницы или <c>null</c>.</returns>
    /// <remarks>
    /// Адрес передаётся строкой, потому что карточка может быть ещё не сохранена (нет идентификатора).
    /// </remarks>
    [Get("/api/probe/title")]
    Task<CardTitleResponse> GetTitleAsync([Query] string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ответ API на запрос заголовка страницы.
/// </summary>
/// <param name="Title">Заголовок страницы или <c>null</c>, если он не найден.</param>
public sealed record CardTitleResponse(string? Title);
