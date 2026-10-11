using System.Security.Cryptography;
using Adashboard.Api.Data;

namespace Adashboard.Api.Services;

/// <summary>
/// Сервис хранения пользовательских изображений-иконок карточек.
/// Файлы сохраняются локально в каталоге загрузок приложения (<c>uploads/card-icons</c>).
/// </summary>
/// <remarks>
/// <para>
/// Этот сервис — единственное место, где backend сам ходит по URL, заданному клиентом,
/// то есть потенциальный SSRF-sink. Проверка приватных и служебных адресов
/// (<c>EnsurePublicHostAsync</c> из прежней версии) здесь намеренно <b>не применяется</b>:
/// доступ к API ограничивается авторизацией, а иконки часто нужны с локальных домашних
/// сервисов, ради которых и существует дашборд. Это тот же осознанный компромисс, что и
/// в <c>ProbeService</c>.
/// </para>
/// <para>
/// Остаются ограничения, не зависящие от доверия к пользователю: только схемы http/https,
/// запрет на скачивание больше <see cref="MaxImageBytes"/> (потоковое чтение), таймаут
/// запроса и проверка сигнатуры растрового изображения (защита от сохранения SVG/HTML).
/// </para>
/// </remarks>
public sealed class ImageIconService(
    IHttpClientFactory httpClientFactory,
    DashboardPaths paths,
    ILogger<ImageIconService> logger)
{
    /// <summary>
    /// Максимальный размер принимаемого изображения в байтах.
    /// </summary>
    public const long MaxImageBytes = 2 * 1024 * 1024;

    private const string HttpClientName = "IconDownloader";
    private const string FolderRelativePath = "uploads/card-icons";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(12);

    private readonly string _folder = Path.Combine(paths.UploadsPath, "card-icons");

    /// <summary>
    /// Скачивает изображение по указанному URL и сохраняет его для карточки.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="url">Абсолютная HTTP(S)-ссылка на изображение.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Относительный путь к сохранённому изображению.</returns>
    public async Task<string> SaveFromUrlAsync(int cardId, string url, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начата загрузка изображения карточки {CardId} по ссылке.", cardId);

        if (cardId <= 0)
        {
            throw new InvalidOperationException("Некорректный идентификатор карточки.");
        }

        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            logger.LogWarning("Ссылка на изображение карточки {CardId} отклонена: некорректный адрес.", cardId);
            throw new InvalidOperationException("Укажите корректную HTTP(S)-ссылку на изображение.");
        }

        using var client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = RequestTimeout;

        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Загрузка изображения карточки {CardId} завершилась неудачно. Код: {StatusCode}.", cardId, (int)response.StatusCode);
            throw new InvalidOperationException("Не удалось загрузить изображение по указанной ссылке.");
        }

        var content = await ReadLimitedAsync(cardId, response.Content, cancellationToken);
        var extension = DetectExtension(content);

        if (extension is null)
        {
            logger.LogWarning("Ссылка на изображение карточки {CardId} ведёт не на растровое изображение.", cardId);
            throw new InvalidOperationException("Ссылка ведёт не на поддерживаемое растровое изображение.");
        }

        var path = await WriteImageAsync(cardId, content, extension, cancellationToken);

        logger.LogInformation("Изображение карточки {CardId} сохранено: {Path}.", cardId, path);
        return path;
    }

    /// <summary>
    /// Сохраняет переданные байты изображения для карточки (загрузка с устройства или из буфера).
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="content">Содержимое изображения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Относительный путь к сохранённому изображению.</returns>
    public async Task<string> SaveFromBytesAsync(int cardId, byte[] content, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато сохранение изображения карточки {CardId} из загруженных данных.", cardId);

        if (cardId <= 0)
        {
            throw new InvalidOperationException("Некорректный идентификатор карточки.");
        }

        if (content.Length == 0 || content.LongLength > MaxImageBytes)
        {
            logger.LogWarning("Изображение карточки {CardId} отклонено: недопустимый размер.", cardId);
            throw new InvalidOperationException("Изображение превышает максимальный размер 2 МБ.");
        }

        var extension = DetectExtension(content);

        if (extension is null)
        {
            logger.LogWarning("Изображение карточки {CardId} отклонено: неподдерживаемый формат.", cardId);
            throw new InvalidOperationException("Файл не является поддерживаемым растровым изображением.");
        }

        var path = await WriteImageAsync(cardId, content, extension, cancellationToken);

        logger.LogInformation("Изображение карточки {CardId} сохранено: {Path}.", cardId, path);
        return path;
    }

    /// <summary>
    /// Удаляет все сохранённые изображения карточки.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    public void Delete(int cardId)
    {
        if (cardId <= 0)
        {
            return;
        }

        try
        {
            RemoveCardFiles(cardId);
            logger.LogInformation("Изображения карточки {CardId} удалены.", cardId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось удалить изображения карточки {CardId}.", cardId);
        }
    }

    /// <summary>
    /// Записывает изображение в хранилище и возвращает относительный путь.
    /// </summary>
    private async Task<string> WriteImageAsync(int cardId, byte[] content, string extension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_folder);

        // Зашитый в имя хеш содержимого меняется при замене картинки и сбрасывает кэш браузера.
        var hash = Convert.ToHexString(SHA256.HashData(content))[..16].ToLowerInvariant();
        var fileName = $"{cardId}-{hash}{extension}";
        var targetPath = Path.Combine(_folder, fileName);
        var tempPath = Path.Combine(_folder, $".tmp-{Guid.NewGuid():N}");

        await File.WriteAllBytesAsync(tempPath, content, cancellationToken);

        try
        {
            // Сначала фиксируем новый файл, и только потом убираем старые: сбой на этом
            // этапе не должен оставить карточку без изображения.
            File.Move(tempPath, targetPath, overwrite: true);
            RemoveCardFiles(cardId, exceptPath: targetPath);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }

        return $"/{FolderRelativePath}/{fileName}";
    }

    /// <summary>
    /// Удаляет файлы всех изображений указанной карточки, кроме файла-исключения.
    /// </summary>
    private void RemoveCardFiles(int cardId, string? exceptPath = null)
    {
        if (!Directory.Exists(_folder))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_folder, $"{cardId}-*"))
        {
            if (exceptPath is not null && string.Equals(path, exceptPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Delete(path);
        }
    }

    /// <summary>
    /// Читает ответ с ограничением размера, чтобы не вычитывать в память большой файл.
    /// </summary>
    private async Task<byte[]> ReadLimitedAsync(int cardId, HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxImageBytes)
            {
                logger.LogWarning("Изображение карточки {CardId} отклонено: превышен максимальный размер.", cardId);
                throw new InvalidOperationException("Изображение превышает максимальный размер 2 МБ.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Определяет расширение изображения по сигнатуре файла.
    /// </summary>
    private static string? DetectExtension(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return ".png";
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ".jpg";
        }

        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
        {
            return ".gif";
        }

        if (data.Length >= 12
            && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
        {
            return ".webp";
        }

        if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D)
        {
            return ".bmp";
        }

        if (data.Length >= 4 && data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01 && data[3] == 0x00)
        {
            return ".ico";
        }

        if (data.Length >= 12 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70)
        {
            var brand = System.Text.Encoding.ASCII.GetString(data.Slice(8, 4));
            if (brand is "avif" or "avis")
            {
                return ".avif";
            }
        }

        return null;
    }
}
