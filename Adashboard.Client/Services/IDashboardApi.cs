using Adashboard.Domain.Models;
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
/// Базовый адрес API (например, <c>http://localhost:5024</c>) задаётся один раз при регистрации
/// клиента в <c>Program.cs</c> через <c>AddRefitClient&lt;IDashboardApi&gt;()</c>. Относительные пути
/// из атрибутов (например, <c>/api/layout</c>) Refit приклеивает к этому базовому адресу.
/// </para>
/// <para>
/// Атрибуты запроса:
/// <list type="bullet">
/// <item><description><c>[Get]</c> — HTTP GET, читает данные. Ответ десериализуется в указанный тип.</description></item>
/// <item><description><c>[Put]</c> — HTTP PUT, полностью заменяет ресурс телом запроса.</description></item>
/// <item><description><c>[Post]</c> — HTTP POST, создаёт ресурс или отправляет данные на обработку.</description></item>
/// <item><description><c>[Delete]</c> — HTTP DELETE, удаляет ресурс.</description></item>
/// <item><description><c>[Body]</c> — помечает параметр как тело запроса (по умолчанию JSON).</description></item>
/// <item><description><c>[Multipart]</c> и <c>[AliasAs]</c> — отправка файла как <c>multipart/form-data</c>; <c>[AliasAs]</c> задаёт имя поля формы.</description></item>
/// </list>
/// </para>
/// <para>
/// Фигурные скобки в пути (<c>{cardId}</c>) — это подстановка значений параметров метода в URL.
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

    /// <summary>
    /// Загружает основную раскладку dashboard (<c>GET /api/layout</c>).
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Раскладка вместе с категориями и карточками; <c>null</c>, если API вернуло пустое тело.</returns>
    [Get("/api/layout")]
    Task<DashboardLayout> GetLayoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Полностью сохраняет основную раскладку dashboard (<c>PUT /api/layout</c>).
    /// </summary>
    /// <param name="layout">Раскладка, которой нужно заменить текущую.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <remarks>
    /// Возвращаемый тип <see cref="Task"/> без результата: при успешном ответе Refit ничего не возвращает,
    /// а при ошибке (например, 400) выбрасывает <see cref="ApiException"/>.
    /// </remarks>
    [Put("/api/layout")]
    Task SaveLayoutAsync([Body] DashboardLayout layout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет изображение-иконку карточки, загруженное с устройства
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

    /// <summary>
    /// Проверяет доступность сервиса карточки на стороне API (<c>GET /api/probe/health/{cardId}</c>).
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Ответ со строковым состоянием и пояснением.</returns>
    /// <remarks>
    /// API сам находит карточку по идентификатору и обращается к её сервису. Браузерный клиент
    /// не может сделать это напрямую: целевые сервисы не отдают CORS-заголовки.
    /// </remarks>
    [Get("/api/probe/health/{cardId}")]
    Task<CardHealthResponse> CheckHealthAsync(int cardId, CancellationToken cancellationToken = default);

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
/// Ответ API на проверку доступности карточки.
/// </summary>
/// <param name="Status">Строковое состояние: <c>online</c>, <c>offline</c> или <c>unknown</c>.</param>
/// <param name="Message">Короткое пояснение исхода для подсказки на карточке.</param>
public sealed record CardHealthResponse(string Status, string? Message);

/// <summary>
/// Ответ API на запрос заголовка страницы.
/// </summary>
/// <param name="Title">Заголовок страницы или <c>null</c>, если он не найден.</param>
public sealed record CardTitleResponse(string? Title);
