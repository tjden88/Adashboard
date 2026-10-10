namespace Adashboard.Services;

/// <summary>
/// Сервис хранения пользовательских изображений-иконок карточек.
/// </summary>
/// <remarks>
/// В WASM-клиенте файлы физически хранит API, а этот интерфейс описывает операции,
/// которые клиент выполняет через эндпоинты <c>/api/cards/{cardId}/image</c>.
/// </remarks>
public interface IImageIconService
{
    /// <summary>
    /// Максимальный размер принимаемого изображения в байтах.
    /// </summary>
    const long MaxImageBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Скачивает изображение по указанному URL и сохраняет его для карточки.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="url">Абсолютная HTTP(S)-ссылка на изображение.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Относительный путь к сохранённому изображению.</returns>
    Task<string> SaveFromUrlAsync(int cardId, string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет переданные байты изображения для карточки (загрузка с устройства или из буфера).
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="content">Содержимое изображения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Относительный путь к сохранённому изображению.</returns>
    Task<string> SaveFromBytesAsync(int cardId, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет все сохранённые изображения карточки.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    void Delete(int cardId);
}
